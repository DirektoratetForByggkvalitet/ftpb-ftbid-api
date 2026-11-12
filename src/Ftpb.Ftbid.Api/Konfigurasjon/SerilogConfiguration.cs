using Elastic.Apm.SerilogEnricher;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

namespace Ftpb.Ftbid.Api.Konfigurasjon;

/// <summary>Konfigurerer konsoll- og Elasticsearch-logging med APM-korrelasjon.</summary>
public static class SerilogConfiguration
{
    /// <summary>Legger til loggberikelse og sender til Elasticsearch når tilkobling og legitimasjon er satt.</summary>
    public static void ConfigureLogging(LoggerConfiguration logging, IConfiguration configuration, string logName)
    {
        var elasticSearchUrl = configuration.GetValue<string>("Serilog:ConnectionUrl");
        var elasticUsername = configuration.GetValue<string>("Serilog:Username");
        var elasticPassword = configuration.GetValue<string>("Serilog:Password");

        logging
            .MinimumLevel.Is(LogEventLevel.Information)
            .MinimumLevel.Override("Elastic.Apm", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithElasticApmCorrelationInfo()
            .WriteTo.Console();

        if (!string.IsNullOrEmpty(elasticSearchUrl) && !string.IsNullOrEmpty(elasticUsername) && !string.IsNullOrEmpty(elasticPassword))
        {
            logging.WriteTo.Elasticsearch(
                [new(elasticSearchUrl)],
                conf => conf.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName(logName),
                tr => tr.Authentication(new BasicAuthentication(elasticUsername, elasticPassword)));
        }
        else
            Console.WriteLine("ERROR IN SERILOG CONFIGURATION - Unable to register elastic sink. URL, username or password is missing in config");

        if (configuration.GetValue<bool>("Serilog:SelfLogEnabled"))
            SelfLog.Enable(msg => Console.WriteLine(msg));
    }
}
