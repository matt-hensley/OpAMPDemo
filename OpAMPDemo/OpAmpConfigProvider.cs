using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpAMPDemo
{
    public class OpAmpConfigSource : IConfigurationSource
    {
        public string ServiceName { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new OpAmpConfigProvider(ServiceName);
        }
    }

    public class OpAmpConfigProvider : ConfigurationProvider, IDisposable
    {
        private readonly string _serviceName;
        private readonly HttpClient _httpClient;
        private readonly Timer _timer;
        private readonly IDeserializer _deserializer;

        public OpAmpConfigProvider(string serviceName)
        {
            _serviceName = serviceName;
            _httpClient = new HttpClient();
            _timer = new Timer(_ => LoadInternal(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance)
                .Build();
        }

        public override void Load()
        {
            // dummy load method, timer immediately calls LoadInternal asynchronously
        }

        internal async void LoadInternal()
        {
            var url = $"http://localhost:12345/api/v0/debugdial/{_serviceName}";
            var resp = await _httpClient.GetAsync(url);
            var res = await resp.Content.ReadAsStringAsync();
            var config = _deserializer.Deserialize<OpAmpConfig>(res);

            foreach (var logLevel in config.logLevels)
            {
                var level = logLevel.level switch
                {
                    "DEBUG" => "Debug",
                    "INFO" => "Information",
                    "WARN" => "Warning",
                    "ERROR" => "Error",
                    "FATAL" => "Critical",
                    _ => "Information"
                };
                // can target Logging:LogLevel:OpenTelemetry:xyz
                Data[$"Logging:LogLevel:{logLevel.logger}"] = level;
            }

            OnReload();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _httpClient.Dispose();
        }
    }
}

public class OpAmpConfig
{
    public class LogLevel
    {
        public string logger { get; set; }
        public string level { get; set; }
    }

    public LogLevel[] logLevels { get; set; }

    public double sampleRatio { get; set; }
}
