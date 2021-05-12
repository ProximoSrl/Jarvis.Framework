# App metrics

Jarvis Framework initially used https://github.com/Recognos/Metrics.NET metrics library, but due to discontinued developing of this library, we decided to move to another library. Some of the original code of https://github.com/Recognos/Metrics.NET library regarding health check was included in Jarvis Framework to avoid including too much breaking changes.

Now Jarvis.Framework uses App.metrics library with a series of wrappers that can helps moving from the old metrics to the new ones. You basically need to reference app.metrics and all the reporter you need, then you need to configure your software calling as soon as possible configuration code like in this example.

```csharp
public static class MetricsConfigurator
{
	public static void Configure(string context)
	{
		var metricsBuilder = new MetricsBuilder()
			.Configuration.Configure(
				options =>
				{
					options.DefaultContextLabel = context;
					//options.GlobalTags.Add("myTagKey", "myTagValue");
					options.Enabled = true;
					options.ReportingEnabled = true;
				})
			.OutputMetrics.AsPlainText()
			.OutputMetrics.AsJson()
			.OutputMetrics.AsPrometheusPlainText()
			.OutputMetrics.AsPrometheusProtobuf()
			.OutputMetrics.AsMetricsNetCompatible()
			.OutputMetrics.AsGrafanaCloudHostedMetricsGraphiteSyntax(TimeSpan.FromSeconds(5));

		//Call jarvis framework metrics helper initialization
		MetricsHelper.InitMetrics(metricsBuilder);
	}
}
```

Initialization code is really similar thanks to Jarvis.Framework helper that can allow the usage of the old Metrics.Net syntax to define the metrics.

## Health Checks

The original code for Metrics.NET was included in the Jarvis.Framework to support Health Checks with the old Metrics.Net syntax.

## Metrics Owin

No metrics owin is supported, you need to create a simple controller that expose the metrics for you as you need, as in following example working with standard Web.API full framework.

```csharp
public class WebapiMetricsControllerUnauthenticatedController : ApiController
{
	private static readonly JsonMediaTypeFormatter _jsonFormatter;

	static WebapiMetricsControllerUnauthenticatedController()
	{
		_jsonFormatter = new JsonMediaTypeFormatter();
		_jsonFormatter.SerializerSettings = new JsonSerializerSettings()
		{
			TypeNameHandling = TypeNameHandling.None
		};
	}

	private const string FlotAppResource = "Jarvis.Common.Shared.FullFw.Utils.WebApi.index.full.html.gz";
	private static readonly Lazy<string> htmlContent = new Lazy<string>(ReadFromEmbededResource);

	private static string ReadFromEmbededResource()
	{
		using (var stream = Assembly.GetAssembly(typeof(WebapiMetricsControllerUnauthenticatedController)).GetManifestResourceStream(FlotAppResource))
		using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
		using (var reader = new StreamReader(gzip))
		{
			return reader.ReadToEnd();
		}
	}

	public static void Configure(IWindsorContainer container)
	{
		container.Register(
			Classes
				.FromAssemblyContaining<WebapiMetricsControllerUnauthenticatedController>()
				.BasedOn<ApiController>()
				.LifestyleTransient());
	}

	[HttpGet]
	[Route("metrics/health")]
	public HttpResponseMessage Health()
	{
		var status = HealthChecks.GetStatus();

		HealthCheckResultDto dto = new HealthCheckResultDto()
		{
			IsHealthy = status.IsHealthy,
		};
		dto.Failed = status.Results.Where(r => !r.Check.IsHealthy)
			.Select(r => new SingleHealthCheckDto(r))
			.ToArray();
		dto.Succeeded = status.Results.Where(r => r.Check.IsHealthy)
			.Select(r => new SingleHealthCheckDto(r))
			.ToArray();
		if (status.IsHealthy)
		{
			return Request.CreateResponse(HttpStatusCode.OK, dto, _jsonFormatter);
		}

		return Request.CreateResponse(HttpStatusCode.InternalServerError, dto, _jsonFormatter);
	}

	[HttpGet]
	[Route("metrics/jsonnative")]
	public Task<HttpResponseMessage> JsonNative()
	{
		return StreamOutputFormat<MetricsJsonOutputFormatter>("application/json");
	}

	[HttpGet]
	[Route("metrics/json")]
	public Task<HttpResponseMessage> Json()
	{
		return StreamOutputFormat<MetricsNetCompatibleJsonOutputFormatter>("application/json");
	}

	[HttpGet]
	[Route("metrics/text")]
	public Task<HttpResponseMessage> Text()
	{
		return StreamOutputFormat<MetricsTextOutputFormatter>("text/plain");
	}

	[HttpGet]
	[Route("metrics/prometheus")]
	public Task<HttpResponseMessage> Prometheus()
	{
		return StreamOutputFormat<MetricsPrometheusTextOutputFormatter>("text/plain");
	}

	private async Task<HttpResponseMessage> StreamOutputFormat<T>(string mediaType)
		where T : IMetricsOutputFormatter
	{
		var snapshot = MetricsHelper.Metrics.Snapshot.Get();
		var textFormatter = MetricsHelper.Metrics.OutputMetricsFormatters.OfType<T>().FirstOrDefault();

		string metrics = null;
		using (var ms = new MemoryStream(1000))
		{
			await textFormatter.WriteAsync(ms, snapshot).ConfigureAwait(false);
			ms.Flush();
			metrics = Encoding.UTF8.GetString(ms.ToArray());
		}

		var response = Request.CreateResponse(HttpStatusCode.OK);
		response.Content = new StringContent(metrics, Encoding.UTF8, mediaType);
		return response;
	}

	[HttpGet]
	[Route("metrics/prometheusprotobuf")]
	public async Task<HttpResponseMessage> PrometheusProtobuf()
	{
		var snapshot = MetricsHelper.Metrics.Snapshot.Get();
		var textFormatter = MetricsHelper.Metrics.OutputMetricsFormatters.OfType<MetricsPrometheusProtobufOutputFormatter>().FirstOrDefault();

		var ms = new MemoryStream(1000);
		await textFormatter.WriteAsync(ms, snapshot).ConfigureAwait(false);
		ms.Flush();

		var response = Request.CreateResponse(HttpStatusCode.OK);
		response.Content = new ByteArrayContent(ms.ToArray());
		response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.google.protobuf");
		return response;
	}

	[HttpGet]
	[Route("metrics/metrics")]
	public HttpResponseMessage Metrics()
	{
		var response = Request.CreateResponse(HttpStatusCode.OK);
		response.Content = new StringContent(htmlContent.Value, Encoding.UTF8, "text/html");
		return response;
	}

	private class HealthCheckResultDto
	{
		public bool IsHealthy { get; set; }

		public SingleHealthCheckDto[] Failed { get; set; }

		public SingleHealthCheckDto[] Succeeded { get; set; }
	}

	private class SingleHealthCheckDto
	{
		public SingleHealthCheckDto(HealthCheck.Result result)
		{
			Message = result.Check.Message;
			Name = result.Name;
			IsHealthy = result.Check.IsHealthy;
		}
		public string Message { get; set; }

		public string Name { get; set; }

		public bool IsHealthy { get; set; }
	}
}
```