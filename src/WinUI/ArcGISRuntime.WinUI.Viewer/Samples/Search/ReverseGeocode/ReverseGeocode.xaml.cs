﻿// Copyright 2019 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using ArcGISRuntime.Samples.Shared.Managers;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.UI.Popups;

namespace ArcGISRuntime.WinUI.Samples.ReverseGeocode
{
    [ArcGISRuntime.Samples.Shared.Attributes.Sample(
        name: "Reverse geocode",
        category: "Search",
        description: "Use an online service to find the address for a tapped point.",
        instructions: "Tap the map to see the nearest address displayed in a callout.",
        tags: new[] { "address", "geocode", "locate", "reverse geocode", "search" })]
    public partial class ReverseGeocode
    {
        // Service Uri to be provided to the LocatorTask (geocoder).
        private readonly Uri _serviceUri = new Uri("https://geocode-api.arcgis.com/arcgis/rest/services/World/GeocodeServer");

        // The LocatorTask provides geocoding services.
        private LocatorTask _geocoder;

        public ReverseGeocode()
        {
            InitializeComponent();
            _ = Initialize();
        }

        private async Task Initialize()
        {
            if (await ApiKeyManager.CheckKeyValidity() != ApiKeyStatus.Valid)
            {
                await new MessageDialog2("Please use the settings dialog to configure an API Key.", "Error").ShowAsync();
                return;
            }

            // Create new Map with basemap.
            Map myMap = new Map(BasemapStyle.ArcGISImagery);

            // Provide used Map to the MapView.
            MyMapView.Map = myMap;

            // Add a graphics overlay to the map for showing where the user tapped.
            MyMapView.GraphicsOverlays.Add(new GraphicsOverlay());

            // Enable tap-for-info pattern on results.
            MyMapView.GeoViewTapped += MyMapView_GeoViewTapped;

            // Initialize the LocatorTask with the provided service Uri.
            try
            {
                _geocoder = await LocatorTask.CreateAsync(_serviceUri);
            }
            catch (Exception e)
            {
                await new MessageDialog2(e.ToString(), "Error configuring geocoder").ShowAsync();
            }

            // Set the initial viewpoint.
            await MyMapView.SetViewpointCenterAsync(34.058, -117.195, 5e4);
        }

        private void MyMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            _ = MyMapView_GeoViewTappedTask(sender, e);
        }

        private async Task MyMapView_GeoViewTappedTask(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                // Clear the existing graphics & callouts.
                MyMapView.DismissCallout();
                MyMapView.GraphicsOverlays[0].Graphics.Clear();

                // Add a graphic for the tapped point.
                Graphic pinGraphic = await GraphicForPoint(e.Location);
                MyMapView.GraphicsOverlays[0].Graphics.Add(pinGraphic);

                // Normalize the geometry - needed if the user crosses the international date line.
                MapPoint normalizedPoint = (MapPoint)GeometryEngine.NormalizeCentralMeridian(e.Location);

                // Reverse geocode to get addresses.
                IReadOnlyList<GeocodeResult> addresses = await _geocoder.ReverseGeocodeAsync(normalizedPoint);

                // Get the first result.
                GeocodeResult address = addresses.First();

                // Use the city and region for the Callout Title.
                string calloutTitle = address.Attributes["Address"].ToString();

                // Use the metro area for the Callout Detail.
                string calloutDetail = address.Attributes["City"] +
                                       " " + address.Attributes["Region"] +
                                       " " + address.Attributes["CountryCode"];

                // Define the callout.
                CalloutDefinition calloutBody = new CalloutDefinition(calloutTitle, calloutDetail);

                // Show the callout on the map at the tapped location.
                MyMapView.ShowCalloutForGeoElement(pinGraphic, e.Position, calloutBody);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await new MessageDialog2("No results found.", "No results").ShowAsync();
            }
        }

        private async Task<Graphic> GraphicForPoint(MapPoint point)
        {
            // Get current assembly that contains the image.
            Assembly currentAssembly = GetType().GetTypeInfo().Assembly;

            // Get image as a stream from the resources.
            // Picture is defined as EmbeddedResource and DoNotCopy.
            Stream resourceStream = currentAssembly.GetManifestResourceStream(
                "ArcGISRuntime.WinUI.Viewer.Resources.PictureMarkerSymbols.pin_star_blue.png");

            // Create new symbol using asynchronous factory method from stream.
            PictureMarkerSymbol pinSymbol = await PictureMarkerSymbol.CreateAsync(resourceStream);
            pinSymbol.Width = 60;
            pinSymbol.Height = 60;
            // The image is a pin; offset the image so that the pinpoint
            //     is on the point rather than the image's true center.
            pinSymbol.LeaderOffsetX = 30;
            pinSymbol.OffsetY = 14;
            return new Graphic(point, pinSymbol);
        }
    }
}