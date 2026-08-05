using Microsoft.AspNetCore.Mvc;
using Fruitables.Services.Communications;
using Fruitables.Services.Shipping.Address;

namespace Fruitables.Controllers;

[ApiController]
[Route("api/address")]
public class AddressApiController : ControllerBase
{
    private readonly IVietnamAddressService _addressService;

    public AddressApiController(IVietnamAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces()
    {
        try
        {
            var provinces = await _addressService.GetProvincesAsync();
            return Ok(provinces);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "ApiTimeout", message = "API không phản hồi trong 10 giây" });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { error = "ServiceUnavailable", message = ex.Message });
        }
    }

    [HttpGet("communes/{provinceId}")]
    public async Task<IActionResult> GetCommunes(string provinceId)
    {
        if (string.IsNullOrWhiteSpace(provinceId))
        {
            return BadRequest(new { error = "InvalidProvinceId", message = "Mã tỉnh không hợp lệ" });
        }

        try
        {
            var communes = await _addressService.GetCommunesByProvinceAsync(provinceId);
            return Ok(communes);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "ApiTimeout", message = "API không phản hồi trong 10 giây" });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new { error = "ServiceUnavailable", message = ex.Message });
        }
    }
}
