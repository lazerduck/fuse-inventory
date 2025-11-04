namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Account>))]
        public async Task<ActionResult<IEnumerable<Account>>> GetAccounts()
        {
            return Ok(await _accountService.GetAccountsAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Account))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Account>> GetAccountById([FromRoute] Guid id)
        {
            var a = await _accountService.GetAccountByIdAsync(id);
            return a is not null ? Ok(a) : NotFound(new { error = $"Account with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Account))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccount command)
        {
            var result = await _accountService.CreateAccountAsync(command);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var account = result.Value!;
            return CreatedAtAction(nameof(GetAccountById), new { id = account.Id }, account);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Account))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Account>> UpdateAccount([FromRoute] Guid id, [FromBody] UpdateAccount command)
        {
            var merged = command with { Id = id };
            var result = await _accountService.UpdateAccountAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteAccount([FromRoute] Guid id)
        {
            var result = await _accountService.DeleteAccountAsync(new DeleteAccount(id));
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }
            return NoContent();
        }
    }
}
