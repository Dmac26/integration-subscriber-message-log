using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Net.Security;
using System.Text;
using Wachter.IntegrationSubscriberMessageLog.Models;
using Wachter.IntegrationSubscriberMessageLog.Resolvers;
//using Microsoft.EntityFrameworkCore.Metadata;

namespace Wachter.IntegrationSubscriberMessageLog.Services
{
	public class MessageLogService : IHostedService, IDisposable
	{
		private readonly ILogger _log = Log.ForContext<MessageLogService>();
		private readonly IntegrationMessageLogDbContext _dbContext;
		private readonly IConfiguration _configuration;

		private IConnection? _connection;
		private IModel? _channel;

		public MessageLogService(IConfiguration configuration, IntegrationMessageLogDbContext dbContext)
		{
			_configuration = configuration;
			_dbContext = dbContext;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			try
			{
				_log.Information("-------------------------------------------------------------------------------------");
				_log.Information("Config VirtualHost: {VirtualHost}", _configuration["RabbitMq:VirtualHost"] ?? "(null)");
				_log.Information("-------------------------------------------------------------------------------------");

                try
                {
                    // Use the relational model to avoid metadata extensions
                    var relational = _dbContext.GetService<IRelationalDatabaseFacadeDependencies>();
                    var entityType = _dbContext.Model.FindEntityType(typeof(MessageLog));

                    if (entityType == null)
                    {
                        _log.Error("EF Core cannot find entity type IntegrationMessageLog - check namespace or registration");
                    }
                    else
                    {
                        var primaryKey = entityType.FindPrimaryKey();
                        if (primaryKey == null)
                        {
                            _log.Error("EF Core reports NO primary key defined for IntegrationMessageLog");
                        }
                        else
                        {
                            var pkProperty = primaryKey.Properties.FirstOrDefault()?.Name;
                            _log.Information("EF Core sees primary key as property: {PkProperty}", pkProperty ?? "none");
                        }

                        _log.Information("Properties EF Core sees for IntegrationMessageLog:");
                        foreach (var prop in entityType.GetProperties())
                        {
                            var columnName = prop.GetColumnName();
                            _log.Information("  Property: {Name} -> Column: {ColumnName}", prop.Name, columnName ?? "no mapping");
                        }

                        // Also log the table name EF is using
                        var tableName = entityType.GetTableName();
                        _log.Information("EF Core is mapping IntegrationMessageLog to table: {TableName}", tableName ?? "none");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error inspecting EF model (this is expected if extensions clash)");
                }

                var secretTitle = "RabbitMq";

				var (username, password) = await KeeperResolver.GetRabbitCredentialsAsync(secretTitle);

				if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
				{
					_log.Warning("Missing username or password from Keeper - connection will fail");
				}

				var factory = new ConnectionFactory
				{
					HostName = _configuration["RabbitMq:HostName"]!,
					Port = int.Parse(_configuration["RabbitMq:Port"]!),
					VirtualHost = _configuration["RabbitMq:VirtualHost"]!,
					UserName = username,
					Password = password,
					Ssl = new SslOption
					{
						Enabled = true,
						AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateNameMismatch |
							 SslPolicyErrors.RemoteCertificateChainErrors |
							 SslPolicyErrors.RemoteCertificateNotAvailable
					}
				};

				_connection = factory.CreateConnection();
				_channel = _connection.CreateModel();
				_channel.BasicQos(0, 1, false);

				var consumer = new EventingBasicConsumer(_channel);
				consumer.Received += Consumer_Received;

				var queueName = "wachter.logging.message.log";
				_channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

				_log.Information("MessageLog service started and consuming from queue {QueueName}", queueName);
			}
			catch (Exception ex)
			{
				_log.Fatal(ex, "MessageLog service failed to start");
			}

			//return Task.CompletedTask;
		}

		private void Consumer_Received(object? model, BasicDeliverEventArgs ea)
		{
			try
			{
				var body = ea.Body.Span;
				var message = Encoding.UTF8.GetString(body);

                var logEntry = new MessageLog
                {
                    MessageLogId = Guid.NewGuid(),
                    Exchange = "wachter.logging.message.log",
                    MessageStatus = "Received",
                    Payload = message,
                    FailureAddressed = false // initial value
                };

                _dbContext.MessageLog.Add(logEntry);
				_dbContext.SaveChanges();

				_channel?.BasicAck(ea.DeliveryTag, false);
				_log.Information("Logged message {MessageLogId}", logEntry.MessageLogId);
			}
			catch (Exception ex)
			{
				_log.Error(ex, "Failed to log message – requeueing");
				_channel?.BasicNack(ea.DeliveryTag, false, true);
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_channel?.Close();
			_connection?.Close();
			_log.Information("MessageLog service stopped");

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			StopAsync(CancellationToken.None).GetAwaiter().GetResult();
		}
	}
}