using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using Newtonsoft.Json;
namespace net6test.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private static readonly string _endpoint = "https://cosmostestnet6.documents.azure.com:443/";

    private readonly IConfiguration _conf;

    public AuthController(ILogger<TestController> logger, IConfiguration conf)
    {
        _logger = logger;
        _conf = conf;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] LoginModel request)
    {
        var dbclient = new CosmosClient(_endpoint, _conf.GetValue<string>("CosmosDBKey"));
        var db = dbclient.GetDatabase("test");
        var container = db.GetContainer("test");
        var foundUser = container.GetItemLinqQueryable<UserDto>(true).Where(_ => _.Username == request.Username).ToList().SingleOrDefault();

        if (foundUser != null)
        {
            return BadRequest("username taken");
        }

        CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);
        var user = new UserDto
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt
        };

        await container.CreateItemAsync(user, new PartitionKey(user.Id));
        var jwt = CreateToken(user);
        return Ok(jwt);
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginModel request)
    {
        var dbclient = new CosmosClient(_endpoint, _conf.GetValue<string>("CosmosDBKey"));
        var db = dbclient.GetDatabase("test");
        var container = db.GetContainer("test");
        var user = container.GetItemLinqQueryable<UserDto>(true).Where(_ => _.Username == request.Username).ToList().SingleOrDefault();
        if (user != null && VerifyPasswordHash(request.Password, user))
        {
            var jwt = CreateToken(user);
            return Ok(jwt);
        }
        return BadRequest("worng username or password");
    }

    private string CreateToken(UserDto user)
    {
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, user.Username)
        };

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_conf.GetValue<string>("SecurityKey")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.Now.AddHours(1), signingCredentials: credentials);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return jwt;
    }
    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }

    private bool VerifyPasswordHash(string password, UserDto user)
    {
        using (var hmac = new HMACSHA512(user.PasswordSalt))
        {
            var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return computedHash.SequenceEqual(user.PasswordHash);
        }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class UserDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        public string Username { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
    }
}