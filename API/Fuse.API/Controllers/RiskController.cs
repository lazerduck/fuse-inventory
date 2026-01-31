namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class RiskController : ControllerBase
    {
        private readonly IRiskService _riskService;

        public RiskController(IRiskService riskService)
        {
            _riskService = riskService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Risk>))]
        public async Task<ActionResult<IEnumerable<Risk>>> GetRisks()
        {
            return Ok(await _riskService.GetRisksAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(Risk))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Risk>> GetRiskById([FromRoute] Guid id)
        {
            var risk = await _riskService.GetRiskByIdAsync(id);
            return risk is not null ? Ok(risk) : NotFound(new { error = $"Risk with ID '{id}' not found." });
        }

        [HttpGet("target/{targetType}/{targetId}")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Risk>))]
        public async Task<ActionResult<IEnumerable<Risk>>> GetRisksByTarget([FromRoute] string targetType, [FromRoute] Guid targetId)
        {
            return Ok(await _riskService.GetRisksByTargetAsync(targetType, targetId));
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Risk))]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Risk>> CreateRisk([FromBody] CreateRisk command)
        {
            var result = await _riskService.CreateRiskAsync(command);
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.Error });
            }

            var risk = result.Value!;
            return CreatedAtAction(nameof(GetRiskById), new { id = risk.Id }, risk);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(Risk))]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<Risk>> UpdateRisk([FromRoute] Guid id, [FromBody] UpdateRisk command)
        {
            var merged = command with { Id = id };
            var result = await _riskService.UpdateRiskAsync(merged);
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
        public async Task<IActionResult> DeleteRisk([FromRoute] Guid id)
        {
            var result = await _riskService.DeleteRiskAsync(new DeleteRisk(id));
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
