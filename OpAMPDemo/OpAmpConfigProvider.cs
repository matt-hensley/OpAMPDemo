using opamp.proto;
using System.Net.Http.Headers;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpAMPDemo
{
    public class OpAmpConfigSource : IConfigurationSource
    {
        public string ServiceInstanceId { get; set; }
        public string ServiceName { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new OpAmpConfigProvider(ServiceInstanceId, ServiceName);
        }
    }

    public class OpAmpConfigProvider : ConfigurationProvider, IDisposable
    {
        public static string AGENT_URL = "http://localhost:12345";

        private readonly string _serviceInstanceId;
        private readonly string _serviceName;
        private readonly HttpClient _httpClient;
        private readonly Timer _timer;
        private readonly IDeserializer _deserializer;

        public OpAmpConfigProvider(string serviceInstanceId, string serviceName)
        {
            _serviceInstanceId = serviceInstanceId;
            _serviceName = serviceName;
            _httpClient = new HttpClient();
            _timer = new Timer(_ => LoadInternalOpAmp(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance)
                .Build();
        }

        public override void Load()
        {
            // dummy load method, timer immediately calls LoadInternal asynchronously
        }
        public void Dispose()
        {
            _timer.Dispose();
            _httpClient.Dispose();
        }

        internal async void LoadInternalHttp()
        {
            var url = $"{AGENT_URL}/api/v0/debugdial/{_serviceName}";
            var resp = await _httpClient.GetAsync(url);
            var res = await resp.Content.ReadAsStringAsync();
            var config = _deserializer.Deserialize<OpAmpConfig>(res);
            applyConfig(config);
        }

        internal async void LoadInternalOpAmp()
        {
            var agentToServer = getAgentToServer(_serviceInstanceId, _serviceName);
            var stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, agentToServer);

            var url = $"{AGENT_URL}/api/v0/debugdial";
            var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-protobuf");
            var res = await _httpClient.PostAsync(url, content);

            var bytes = await res.Content.ReadAsStreamAsync();
            var serverToAgent = ProtoBuf.Serializer.Deserialize<ServerToAgent>(bytes);
            var config = extractRemoteConfig(serverToAgent, _serviceName);
            applyConfig(config);
        }

        internal void applyConfig(OpAmpConfig config)
        {
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

            if (config.sampleRatio.HasValue)
            {
                Data["OpAmp:SampleRatio"] = config.sampleRatio.ToString();
            }

            OnReload();
        }

        private opamp.proto.AgentToServer getAgentToServer(string serviceInstanceId, string serviceName)
        {
            return new opamp.proto.AgentToServer
            {
                InstanceUid = serviceInstanceId,
                AgentDescription = new opamp.proto.AgentDescription
                {
                    IdentifyingAttributes =
                    {
                        new opamp.proto.KeyValue
                        {
                            Key = "service.name",
                            Value = new opamp.proto.AnyValue
                            {
                                StringValue = serviceName
                            }
                        }
                    }
                }
            };
        }

        private OpAmpConfig extractRemoteConfig(opamp.proto.ServerToAgent msg, string serviceName)
        {
            if (msg.RemoteConfig.Config.ConfigMaps.ContainsKey(serviceName) == false)
            {
                return new OpAmpConfig();
            }

            var agentConfigFile = msg.RemoteConfig.Config.ConfigMaps[serviceName];

            if (agentConfigFile == null)
            {
                return new OpAmpConfig();
            }

            var s = Encoding.UTF8.GetString(agentConfigFile.Body, 0, agentConfigFile.Body.Length);
            var config = _deserializer.Deserialize<OpAmpConfig>(s);
            return config;
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

    public double? sampleRatio { get; set; }
}
