![Icon](https://raw.githubusercontent.com/davetimmins/ArcGIS.PCL/master/gateway.png)
# ArcGIS.PCL

Use ArcGIS Server REST resources without an official SDK [more information](http://davetimmins.com/2013/July/ArcGIS-PCL/).

It can also be used for just working with types and as well as some ArcGIS Server types you can also use GeoJSON FeatureCollections with the ability to convert GeoJSON <-> ArcGIS Features.

Typical use case would be the need to call some ArcGIS REST resource from server .NET code or maybe a console app. Rather than having to fudge a dependency to an existing SDK you can use this. 
Should work with .NET for Windows Store apps, .NET framework 4.5, Silverlight 5, Windows Phone 8 and higher and Xamarin iOS and Android.

Since the serialization is specific to your implementation you will need to create an ISerializer to use in your gateway. There are NuGet packages created for 2 of these called `ArcGIS.PCL.JsonDotNetSerializer` and `ArcGIS.PCL.ServiceStackV3Serializer`. To use one of these add a reference using NuGet then call the static `Init()` method e.g. `ArcGIS.ServiceModel.Serializers.JsonDotNetSerializer.Init()`. This will create an `ISerializer` instance and override the `SerializerFactory.Get()` method so that it is returned when requested. This also means that you no longer have to pass the `ISerializer` to your gateway or token providers when initialising them, though you can still use this mechanism if you prefer. 

Supports the following as typed operations:

 - Generate Token (automatically if credentials are specified in gateway)
 - Query (attributes, spatial, count, objects Ids)
 - Find
 - Apply Edits
 - Single Input Geocode
 - Suggest
 - Reverse Geocode
 - Describe site (returns a url for every service)
 - Simplify
 - Project
 - Buffer

Some example of it in use for server side processing in web sites

 - [Describe site] (https://arcgissitedescriptor.azurewebsites.net/)
 - [Convert between GeoJSON and ArcGIS Features] (http://arcgisgeojson.azurewebsites.net/)
 - [Server side geometry operations] (http://eqnz.azurewebsites.net/)
 - [Server side geocode] (http://loc8.azurewebsites.net/map?text=wellington, new zealand)
 
The code for these can be seen at [ArcGIS.PCL-Sample-Projects](https://github.com/davetimmins/ArcGIS.PCL-Sample-Projects)

See some of the [tests](https://github.com/davetimmins/ArcGIS.PCL/blob/dev/ArcGIS.Test/ArcGISGatewayTests.cs) for some example calls.

###Gateway Use Cases

#### ArcGIS Server with non secure resources
```csharp
public class ArcGISGateway : PortalGateway
{
    public ArcGISGateway()
        : base(@"http://sampleserver3.arcgisonline.com/ArcGIS/")
    { }
}

... new ArcGISGateway();
```
#### ArcGIS Server with secure resources
```csharp
public class SecureGISGateway : SecureArcGISServerGateway
{
    public SecureGISGateway()
        : base(@"http://serverapps10.esri.com/arcgis", "user1", "pass.word1")
    { }
}

... new SecureGISGateway();
```
#### ArcGIS Server with secure resources and token service at different location
```csharp
public class SecureTokenProvider : TokenProvider
{
    public SecureTokenProvider()
        : base(@"http://serverapps10.esri.com/arcgis", "user1", "pass.word1")
    { }
}

public class SecureGISGateway : PortalGateway
{
    public SecureGISGateway(ISerializer serializer, ITokenProvider tokenProvider)
        : base(@"http://serverapps10.esri.com/arcgis", serializer, tokenProvider)
    { }
}

... new SecureGISGateway(serializer, new SecureTokenProvider(serializer));
```

#### ArcGIS Online either secure or non secure  
```csharp
... new ArcGISOnlineGateway();
 
... new ArcGISOnlineGateway(new ArcGISOnlineTokenProvider("user", "pass", serializer));
```
### Converting between ArcGIS Feature Set from hosted FeatureService and GeoJSON FeatureCollection
```csharp
static ISerializer _serializer = new ServiceStackSerializer();
static Dictionary<String, Func<String, FeatureCollection<IGeoJsonGeometry>>> _funcMap = new Dictionary<String, Func<String, FeatureCollection<IGeoJsonGeometry>>>
{
    { GeometryTypes.Point, (uri) => new ProxyGateway(uri, _serializer).GetGeoJson<Point>(uri) },
    { GeometryTypes.MultiPoint, (uri) => new ProxyGateway(uri, _serializer).GetGeoJson<MultiPoint>(uri) },
    { GeometryTypes.Envelope, (uri) => new ProxyGateway(uri, _serializer).GetGeoJson<Extent>(uri) },
    { GeometryTypes.Polygon, (uri) => new ProxyGateway(uri, _serializer).GetGeoJson<Polygon>(uri) },
    { GeometryTypes.Polyline, (uri) => new ProxyGateway(uri, _serializer).GetGeoJson<Polyline>(uri) }
};

...

var layer = new ProxyGateway(uri, _serializer).GetAnything(uri.AsEndpoint());
if (layer == null || !layer.ContainsKey("geometryType")) throw new HttpException("You must enter a valid layer url.");
return _funcMap[layer["geometryType"]](uri);

...

public class AgsObject : JsonObject, IPortalResponse
{
    [System.Runtime.Serialization.DataMember(Name = "error")]
    public ArcGISError Error { get; set; }
}

public class ProxyGateway : PortalGateway
{
    public ProxyGateway(String rootUrl, ISerializer serializer)
        : base(rootUrl, serializer)
    { }

    public Task<QueryResponse<T>> Query<T>(Query queryOptions) where T : IGeometry
    {
        return Get<QueryResponse<T>, Query>(queryOptions);
    }

    public Task<AgsObject> GetAnything(ArcGISServerEndpoint endpoint)
    {
        return Get<AgsObject>(endpoint);
    }

    public FeatureCollection<IGeoJsonGeometry> GetGeoJson<T>(String uri) where T : IGeometry
    {
        var result = Query<T>(new Query(uri.AsEndpoint())).Result;
        result.Features.First().Geometry.SpatialReference = result.SpatialReference;
        var features = result.Features.ToList();
        if (result.SpatialReference.Wkid != SpatialReference.WGS84.Wkid)
            features = new ProjectGateway(Serializer).Project<T>(features, SpatialReference.WGS84);
        return features.ToFeatureCollection();
    }
}

public class ProjectGateway : PortalGateway
{
    public ProjectGateway(ISerializer serializer)
        : base(@"http://services.arcgisonline.co.nz/ArcGIS/", serializer)
    { }

    public List<Feature<T>> Project<T>(List<Feature<T>> features, SpatialReference outputSpatialReference) where T : IGeometry
    {
        var op = new ProjectGeometry<T>("/Utilities/Geometry/GeometryServer".AsEndpoint(), features, outputSpatialReference);
        var projected = await Post<GeometryOperationResponse<T>, ProjectGeometry<T>>(op);

        var result = features.UpdateGeometries<T>(projected.Geometries);
        if (result.First().Geometry.SpatialReference == null) result.First().Geometry.SpatialReference = outputSpatialReference;
        return result;
    }
}

```
### Converting between GeoJSON FeatureCollection and ArcGIS Feature Set
```csharp
static Dictionary<String, Func<String, List<Feature<IGeometry>>>> _funcMap = new Dictionary<String, Func<String, List<Feature<IGeometry>>>>
{
    { "Point", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonPoint>>(data).ToFeatures<GeoJsonPoint>() },
    { "MultiPoint", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonLineString>>(data).ToFeatures<GeoJsonLineString>() },
    { "LineString", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonLineString>>(data).ToFeatures<GeoJsonLineString>() },
    { "MultiLineString", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonLineString>>(data).ToFeatures<GeoJsonLineString>() },
    { "Polygon", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonPolygon>>(data).ToFeatures<GeoJsonPolygon>() },
    { "MultiPolygon", (data) => JsonSerializer.DeserializeFromString<FeatureCollection<GeoJsonMultiPolygon>>(data).ToFeatures<GeoJsonMultiPolygon>() }
};

...

return _funcMap["Point"](data);
```

### Usage
If you have [NuGet](http://nuget.org) installed, the easiest way to get started is to install via NuGet:

    PM> Install-Package ArcGIS.PCL

On Xamarin you can add the [ArcGIS.PCL component](http://components.xamarin.com/view/ArcGIS.PCL) from the component store or use the [NuGet addin](https://github.com/mrward/monodevelop-nuget-addin) and add it from there.

Of course you can also get the code from this site.

### Icon

[Gateway](http://thenounproject.com/term/gateway/5477/) designed by [Piotr Gawiński](http://thenounproject.com/Piotrek/) from The Noun Project
