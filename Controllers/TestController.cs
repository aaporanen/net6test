using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
namespace net6test.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private static readonly string _endpoint = "https://cosmostestnet6.documents.azure.com:443/";

    private readonly IConfiguration _conf;

    public TestController(ILogger<TestController> logger, IConfiguration conf)
    {
        _logger = logger;
        _conf = conf;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var dbclient = new CosmosClient(_endpoint, _conf.GetValue<string>("CosmosDBKey"));
        var db = dbclient.GetDatabase("test");
        var container = db.GetContainer("test");
        var items = container.GetItemLinqQueryable<Test>(true).ToList();
        return Ok(items);
    }

    public class Test
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}