using Fruitables.Data;
using Microsoft.EntityFrameworkCore;
using Fruitables.Models;
using Fruitables.Services.Communications;
using AddressEntity = Fruitables.Models.Address;

namespace Fruitables.Services.Shipping.Address;

/// <summary>
/// Service for managing user addresses
/// </summary>
public class AddressService : IAddressService
{
    private readonly ApplicationDbContext _db;
    private readonly IVietnamAddressService _vietnamAddressService;

    public AddressService(ApplicationDbContext db, IVietnamAddressService vietnamAddressService)
    {
        _db = db;
        _vietnamAddressService = vietnamAddressService;
    }

    /// <summary>
    /// Gets all addresses for a specific user, ordered by default flag and creation date
    /// </summary>
    public async Task<List<AddressEntity>> GetUserAddressesAsync(int userId)
    {
        return await _db.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific address by ID
    /// </summary>
    public async Task<AddressEntity?> GetAddressByIdAsync(int id)
    {
        return await _db.Addresses.FindAsync(id);
    }

    /// <summary>
    /// Gets the default address for a user
    /// </summary>
    public async Task<AddressEntity?> GetDefaultAddressAsync(int userId)
    {
        return await _db.Addresses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
    }

    /// <summary>
    /// Creates a new address
    /// </summary>
    public async Task<AddressEntity> CreateAddressAsync(AddressEntity address)
    {
        // Trim all string fields
        address.FullName = address.FullName?.Trim() ?? string.Empty;
        address.Phone = address.Phone?.Trim() ?? string.Empty;
        address.ProvinceName = address.ProvinceName?.Trim() ?? string.Empty;
        address.CommuneName = address.CommuneName?.Trim() ?? string.Empty;
        address.StreetAddress = address.StreetAddress?.Trim() ?? string.Empty;
        
        // Sanitize StreetAddress to prevent XSS
        address.StreetAddress = _vietnamAddressService.SanitizeStreetAddress(address.StreetAddress);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(address.FullName))
            throw new ArgumentException("FullName is required", nameof(address));
        if (address.FullName.Length > 200)
            throw new ArgumentException("FullName cannot exceed 200 characters", nameof(address));

        if (string.IsNullOrWhiteSpace(address.Phone))
            throw new ArgumentException("Phone is required", nameof(address));
        if (address.Phone.Length > 20)
            throw new ArgumentException("Phone cannot exceed 20 characters", nameof(address));

        if (string.IsNullOrWhiteSpace(address.StreetAddress))
            throw new ArgumentException("StreetAddress is required", nameof(address));
        if (address.StreetAddress.Length > 200)
            throw new ArgumentException("StreetAddress cannot exceed 200 characters", nameof(address));

        if (string.IsNullOrWhiteSpace(address.ProvinceCode))
            throw new ArgumentException("ProvinceCode is required", nameof(address));
        if (string.IsNullOrWhiteSpace(address.CommuneCode))
            throw new ArgumentException("CommuneCode is required", nameof(address));

        // Set creation time
        address.CreatedAt = DateTime.UtcNow;

        // If this is the first address for the user, make it default
        if (address.UserId.HasValue)
        {
            var existingAddresses = await GetUserAddressesAsync(address.UserId.Value);
            if (!existingAddresses.Any())
            {
                address.IsDefault = true;
            }
        }

        await _db.Addresses.AddAsync(address);
        await _db.SaveChangesAsync();

        return address;
    }

    /// <summary>
    /// Updates an existing address
    /// </summary>
    public async Task<AddressEntity> UpdateAddressAsync(AddressEntity address)
    {
        var existing = await _db.Addresses.FindAsync(address.Id);
        if (existing == null)
            throw new InvalidOperationException($"Address with ID {address.Id} not found");

        // Update fields with sanitization for StreetAddress
        existing.FullName = address.FullName;
        existing.Phone = address.Phone;
        existing.ProvinceCode = address.ProvinceCode;
        existing.ProvinceName = address.ProvinceName;
        existing.CommuneCode = address.CommuneCode;
        existing.CommuneName = address.CommuneName;
        existing.StreetAddress = _vietnamAddressService.SanitizeStreetAddress(address.StreetAddress);
        existing.IsDefault = address.IsDefault;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return existing;
    }

    /// <summary>
    /// Deletes an address
    /// </summary>
    public async Task<bool> DeleteAddressAsync(int id)
    {
        var address = await _db.Addresses.FindAsync(id);
        if (address == null)
            return false;

        // If this was the default address, set another one as default
        if (address.IsDefault && address.UserId.HasValue)
        {
            var otherAddresses = await _db.Addresses
                .Where(a => a.UserId == address.UserId && a.Id != id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            if (otherAddresses.Any())
            {
                otherAddresses.First().IsDefault = true;
            }
        }

        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Sets an address as the default for a user
    /// Automatically clears the default flag from other addresses
    /// </summary>
    public async Task<bool> SetDefaultAddressAsync(int userId, int addressId)
    {
        // Get the address to set as default
        var targetAddress = await _db.Addresses.FindAsync(addressId);
        if (targetAddress == null || targetAddress.UserId != userId)
            return false;

        // Clear default flag from all other addresses for this user
        var userAddresses = await _db.Addresses
            .Where(a => a.UserId == userId)
            .ToListAsync();

        foreach (var addr in userAddresses)
        {
            addr.IsDefault = (addr.Id == addressId);
        }

        await _db.SaveChangesAsync();

        return true;
    }
}
