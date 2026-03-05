namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class MessageBrokerController : ControllerBase
    {
        private readonly IMessageBrokerService _messageBrokerService;

        public MessageBrokerController(IMessageBrokerService messageBrokerService)
        {
            _messageBrokerService = messageBrokerService;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "messageBrokerAll")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<MessageBroker>))]
        public async Task<ActionResult<IEnumerable<MessageBroker>>> GetMessageBrokers()
        {
            return Ok(await _messageBrokerService.GetMessageBrokersAsync());
        }

        [HttpGet("{id}")]
        [SwaggerOperation(OperationId = "messageBrokerGET")]
        [ProducesResponseType(200, Type = typeof(MessageBroker))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<MessageBroker>> GetMessageBrokerById([FromRoute] Guid id)
        {
            var broker = await _messageBrokerService.GetMessageBrokerByIdAsync(id);
            return broker is not null ? Ok(broker) : NotFound(new { error = $"Message broker with ID '{id}' not found." });
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "messageBrokerPOST")]
        [ProducesResponseType(201, Type = typeof(MessageBroker))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<MessageBroker>> CreateMessageBroker([FromBody] CreateMessageBroker command)
        {
            var result = await _messageBrokerService.CreateMessageBrokerAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var broker = result.Value!;
            return CreatedAtAction(nameof(GetMessageBrokerById), new { id = broker.Id }, broker);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(OperationId = "messageBrokerPUT")]
        [ProducesResponseType(200, Type = typeof(MessageBroker))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<MessageBroker>> UpdateMessageBroker([FromRoute] Guid id, [FromBody] UpdateMessageBroker command)
        {
            var merged = command with { Id = id };
            var result = await _messageBrokerService.UpdateMessageBrokerAsync(merged);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.NotFound => NotFound(new { error = result.Error }),
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            return Ok(result.Value);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "messageBrokerDELETE")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteMessageBroker([FromRoute] Guid id)
        {
            var result = await _messageBrokerService.DeleteMessageBrokerAsync(new DeleteMessageBroker(id));
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
