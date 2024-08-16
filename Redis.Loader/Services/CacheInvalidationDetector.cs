
using Google.Protobuf;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Http.Logging;

namespace Redis.Loader.Services
{
    public class CacheInvalidationDetector : BackgroundService
    {
        private readonly ILogger<CacheInvalidationDetector> _logger;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDistributedCache _cache;

        public CacheInvalidationDetector(
            ILogger<CacheInvalidationDetector> logger,
            IConnectionMultiplexer connectionMultiplexer,
            IDistributedCache cache)
        {
            _logger = logger;
            _connectionMultiplexer = connectionMultiplexer;
            _cache = cache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                RedisChannel invalidationChannel = new("__redis__:invalidate", RedisChannel.PatternMode.Auto);
                ISubscriber subscriber = _connectionMultiplexer.GetSubscriber();
                subscriber.Subscribe(invalidationChannel)?.OnMessage(OnMessage);
                IDatabase database = _connectionMultiplexer.GetDatabase();
                var clientList = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First()).ClientList();
                var subscriptionClient = clientList.FirstOrDefault(c => c.Name == "Redis.Loader" && c.SubscriptionCount > 0);
                await database.ExecuteAsync("CLIENT", "TRACKING", "on", "REDIRECT", subscriptionClient.Id.ToString(), "BCAST");
                _logger.LogInformation("Client tracking enabled with invalidation messages.");

                WeatherForecast weatherForecast = new()
                {
                    Date = DateOnly.FromDateTime(DateTime.Now),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = "Summary"
                };
                await _cache.SetStringAsync("weather-forecast", JsonSerializer.Serialize(weatherForecast));
                _logger.LogInformation($"WeatherForecast: {weatherForecast}");

                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Looping in CacheInvalidationDetector...");
                    await Task.Delay(5000);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("CacheInvalidationDetector is stopping.");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Error while attempting to loop in CacheIinvalidationDetector.");
            }
        }

        private void OnMessage(ChannelMessage message)
        {
            try
            {
                _logger.LogInformation($"Key '{message.Message.ToString()}' changed!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error RedisInvalidator.OnMessage: {message}", ex.Message);
            }
        }
    }
}
