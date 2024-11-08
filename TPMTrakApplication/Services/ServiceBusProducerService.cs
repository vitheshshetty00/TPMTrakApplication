using System;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;


namespace TPMTrakApplication.Services;

    public class ServiceBusProducerService : IHostedService, IDisposable
    {
        private readonly ILogger<ServiceBusProducerService> _logger;
        private readonly IConfiguration _configuration;
        private ServiceBusClient? _client;
        private ServiceBusSender? _sender;
        private Timer? _timer;
        private readonly Queue<string> _recentMessages;
        private readonly object _lock = new();

        public bool IsRunning { get; private set; }
        public int MessageCount { get; private set; }
        public IReadOnlyCollection<string> RecentMessages => _recentMessages.ToList().AsReadOnly();

        public ServiceBusProducerService(
            ILogger<ServiceBusProducerService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _recentMessages = new Queue<string>(100);
        }
        

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var connectionString = _configuration["ConnectionStrings:ServiceBusConnectionString"];

            var queueName = "tpmtrak-queue";

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(queueName))
            {
                throw new InvalidOperationException("ServiceBus configuration is missing");
            }

            _client = new ServiceBusClient(connectionString);
            _sender =  _client.CreateSender(queueName);
            
            _logger.LogInformation("Service Bus Producer Service started");
            return;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            if (_sender != null) await _sender.DisposeAsync();
            if (_client != null) await _client.DisposeAsync();
            _logger.LogInformation("Service Bus Producer Service stopped");
        }

        public void Start()
        {
            if (IsRunning) return;

            lock (_lock)
            {
                IsRunning = true;
                _timer = new Timer(async _ => await ProduceMessage(), null, 0, 1000);
                _logger.LogInformation("Message production started");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            lock (_lock)
            {
                IsRunning = false;
                _timer?.Dispose();
                _timer = null;
                _logger.LogInformation("Message production stopped");
            }
        }

        private async Task ProduceMessage()
        {
            try
            {
                var message = GenerateMessage();
                await SendMessageAsync(message);

                lock (_lock)
                {
                    MessageCount++;
                    if (_recentMessages.Count >= 100)
                        _recentMessages.Dequeue();
                    _recentMessages.Enqueue(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error producing message");
                Stop();
            }
        }

        private string GenerateMessage()
        {
            var random = new Random();
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddSeconds(30);
            
            var machineId = random.Next(1, 5);  // Random machine 1-4
            var partId = random.Next(9000, 9020);  // Random part number
            var batchSize = random.Next(5, 51);  // Random batch 5-50
            var operatorId = random.Next(1000, 2000);  // Random operator ID
            
            return $"START-{machineId}-{random.Next(1, 4)}-[{partId}]-{batchSize}-{operatorId}-2-{startTime:yyyyMMdd-HHmmss}-{endTime:yyyyMMdd-HHmmss}-END";
        }

        public async Task SendMessageAsync(string message)
        {
            if (_sender == null)
                throw new InvalidOperationException("Service Bus sender not initialized");

            var serviceBusMessage = new ServiceBusMessage(message);
            await _sender.SendMessageAsync(serviceBusMessage);
            _logger.LogInformation("Sent message: {Message}", message);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _client?.DisposeAsync().GetAwaiter().GetResult();
            _sender?.DisposeAsync().GetAwaiter().GetResult();
        }
    }


