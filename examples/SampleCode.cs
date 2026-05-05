using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;

namespace ArchitectureAnalysis.Sample
{
    // Models
    public class Order
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public string CustomerEmail { get; set; }
        public List<string> Items { get; set; } = new();

        public bool IsValid()
        {
            // Basic validation, more complex rules could be in a separate validator class
            return Items != null && Items.Count > 0 && !string.IsNullOrEmpty(CustomerEmail);
        }
    }

    // Abstractions for Dependencies
    public interface IOrderRepository
    {
        void Save(Order order);
    }

    public interface INotificationService
    {
        void Notify(string recipient, string subject, string message);
    }

    public interface ILogger
    {
        void LogInfo(string message);
        void LogError(string message, Exception ex = null);
    }

    // Concrete Implementations (Examples)
    public class FileOrderRepository : IOrderRepository
    {
        private readonly string _filePath;

        public FileOrderRepository(string filePath = "orders.log")
        {
            _filePath = filePath;
        }

        public void Save(Order order)
        {
            // In a real scenario, this would be more robust, perhaps JSON or XML serialization
            File.AppendAllText(_filePath, $"Order {order.Id} saved at {DateTime.Now} with status: {order.Status}\n");
        }
    }

    public class SmtpNotificationService : INotificationService
    {
        private readonly SmtpClient _smtpClient;
        private readonly string _fromAddress;

        public SmtpNotificationService(string smtpHost, int smtpPort, string fromAddress, string username, string password)
        {
            _smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new System.Net.NetworkCredential(username, password),
                EnableSsl = true // Assuming SSL is needed
            };
            _fromAddress = fromAddress;
        }

        public void Notify(string recipient, string subject, string message)
        {
            try
            {
                using (var mailMessage = new MailMessage(_fromAddress, recipient, subject, message))
                {
                    _smtpClient.Send(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log the error, but don't prevent the order processing from completing if possible
                Console.WriteLine($"Error sending email to {recipient}: {ex.Message}"); // Replace with actual logging
            }
        }
    }

    public class ConsoleLogger : ILogger
    {
        public void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
        }

        public void LogError(string message, Exception ex = null)
        {
            Console.WriteLine($"[ERROR] {DateTime.Now}: {message}");
            if (ex != null)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }

    // Core Business Logic Processor
    public class OrderProcessor
    {
        private readonly IOrderRepository _repository;
        private readonly INotificationService _notifier;
        private readonly ILogger _logger;

        // Constructor Injection for all dependencies
        public OrderProcessor(IOrderRepository repository, INotificationService notifier, ILogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ProcessOrder(Order order)
        {
            if (order == null)
            {
                _logger.LogError("Attempted to process a null order.");
                throw new ArgumentNullException(nameof(order));
            }

            if (!order.IsValid())
            {
                _logger.LogError($"Order {order.Id} is invalid. Items: {order.Items?.Count}, CustomerEmail: {order.CustomerEmail}");
                throw new ArgumentException("Order is invalid.");
            }

            try
            {
                order.Status = "Processed";
                _logger.LogInfo($"Order {order.Id} status set to Processed.");

                _repository.Save(order);
                _logger.LogInfo($"Order {order.Id} saved to repository.");

                _notifier.Notify(order.CustomerEmail, "Order Processed", $"Your order {order.Id} has been successfully processed.");
                _logger.LogInfo($"Notification sent to {order.CustomerEmail} for order {order.Id}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process order {order.Id}.", ex);
                // Depending on requirements, you might want to re-throw, handle differently, or attempt rollback
                throw; 
            }
        }
    }
}