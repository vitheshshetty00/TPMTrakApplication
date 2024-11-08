using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TPMTrakApplication.Services;

namespace TPMTrakApplication.Pages
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ProduceModel : PageModel
    {
        private readonly ServiceBusProducerService _serviceBusProducerService;
        private readonly IConfiguration _configuration;

        public bool IsRunning { get; private set; }
        public int MessageCount { get; private set; }
        public List<string> Messages { get; private set; } = new List<string>();

        public ProduceModel(ServiceBusProducerService serviceBusProducerService, IConfiguration configuration)
        {
            _serviceBusProducerService = serviceBusProducerService;
            _configuration = configuration;
        }

        public void OnGet()
        {
            IsRunning = _serviceBusProducerService.IsRunning;
            MessageCount = _serviceBusProducerService.MessageCount;
            Messages = _serviceBusProducerService.RecentMessages.ToList();
        }

        public IActionResult OnPostStart()
        {
            _serviceBusProducerService.Start();
            return RedirectToPage();
        }

        public IActionResult OnPostStop()
        {
            _serviceBusProducerService.Stop();
            return RedirectToPage();
        }
        public JsonResult OnGetStatus()
        {
            return new JsonResult(new
            {
                messageCount = _serviceBusProducerService.MessageCount,
                latestMessage = _serviceBusProducerService.RecentMessages.FirstOrDefault()
            });
        }
    }
}