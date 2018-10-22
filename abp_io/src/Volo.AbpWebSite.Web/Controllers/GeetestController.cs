using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Geetest.Core;

namespace Volo.AbpWebSite
{
    [Produces("application/json")]
    [Route("api/Geetest")]
    public class GeetestController : Controller
    {
        private readonly IGeetestManager _geetestManager;

        public GeetestController(IGeetestManager geetestManager)
        {
            _geetestManager = geetestManager;
        }

        // GET: api/Geetest
        [HttpGet]
        public async Task<GeetestRegisterResult> Register()
        {
            return await _geetestManager.RegisterAsync();
        }
    }
}