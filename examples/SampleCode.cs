using System;
using System.IO;
using System.Net.Mail;

namespace ArchitectureAnalysis.Sample
{
    // Fora do padrão (Violação de SRP, OCP, DIP)
    // - SRP: Lida com regras de negócio, persistência (banco e arquivo) e envio de email.
    // - OCP: Se houver outro tipo de log ou notificação, teremos que modificar essa classe.
    // - DIP: Instanciação direta de dependências (SmtpClient, File, SqlConnection).
    public class OrderProcessor
    {
        public void ProcessOrder(Order order)
        {
            // Validação de negócio misturada com fluxo principal
            if (order == null || order.Items.Count == 0)
                throw new Exception("Order is invalid");

            // Processamento simulado
            order.Status = "Processed";

            // Persistência acoplada
            File.WriteAllText("log.txt", $"Order {order.Id} processed at {DateTime.Now}");

            // Envio de email acoplado
            SmtpClient client = new SmtpClient("smtp.mailtrap.io");
            client.Send("system@shop.com", order.CustomerEmail, "Order Processed", "Your order was processed!");
        }
    }

    // No padrão (SRP, DIP, Strategy/Observer ready)
    // - Depende de abstrações ao invés de implementações (DIP).
    // - Focado apenas em processar a ordem (SRP).
    public class ModernOrderProcessor
    {
        private readonly IOrderRepository _repository;
        private readonly INotificationService _notifier;

        public ModernOrderProcessor(IOrderRepository repository, INotificationService notifier)
        {
            _repository = repository;
            _notifier = notifier;
        }

        public void ProcessOrder(Order order)
        {
            if (!order.IsValid())
                throw new ArgumentException("Order is invalid");

            order.Status = "Processed";
            
            _repository.Save(order);
            _notifier.Notify(order.CustomerEmail, "Your order was processed!");
        }
    }

    // Models e Interfaces de suporte
    public class Order 
    { 
        public int Id { get; set; }
        public string Status { get; set; }
        public string CustomerEmail { get; set; }
        public System.Collections.Generic.List<string> Items { get; set; } = new();

        public bool IsValid() => Items.Count > 0;
    }

    public interface IOrderRepository { void Save(Order order); }
    public interface INotificationService { void Notify(string to, string message); }
}
