namespace Fuse.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Fuse.Core.Interfaces;
    using Fuse.Core.Models;
    using Fuse.Core.Commands;
    using Fuse.Core.Helpers;

    [ApiController]
    [Route("api/[controller]")]
    public class DataStoreController : ControllerBase
    {
        private readonly IDataStoreService _dataStoreService;

        public DataStoreController(IDataStoreService dataStoreService)
        {
            _dataStoreService = dataStoreService;
        }

        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<DataStore>))]
        public async Task<ActionResult<IEnumerable<DataStore>>> GetDataStores()
        {
            return Ok(await _dataStoreService.GetDataStoresAsync());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(200, Type = typeof(DataStore))]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DataStore>> GetDataStoreById([FromRoute] Guid id)
        {
            var d = await _dataStoreService.GetDataStoreByIdAsync(id);
            return d is not null ? Ok(d) : NotFound(new { error = $"Data store with ID '{id}' not found." });
        }

        [HttpPost]
        [ProducesResponseType(201, Type = typeof(DataStore))]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<DataStore>> CreateDataStore([FromBody] CreateDataStore command)
        {
            var result = await _dataStoreService.CreateDataStoreAsync(command);
            if (!result.IsSuccess)
            {
                return result.ErrorType switch
                {
                    ErrorType.Conflict => Conflict(new { error = result.Error }),
                    _ => BadRequest(new { error = result.Error })
                };
            }

            var ds = result.Value!;
            return CreatedAtAction(nameof(GetDataStoreById), new { id = ds.Id }, ds);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(200, Type = typeof(DataStore))]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<DataStore>> UpdateDataStore([FromRoute] Guid id, [FromBody] UpdateDataStore command)
        {
            var merged = command with { Id = id };
            var result = await _dataStoreService.UpdateDataStoreAsync(merged);
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
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteDataStore([FromRoute] Guid id)
        {
            var result = await _dataStoreService.DeleteDataStoreAsync(new DeleteDataStore(id));
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
