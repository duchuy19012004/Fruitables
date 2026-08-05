using Fruitables.Models;
using AddressEntity = Fruitables.Models.Address;

namespace Fruitables.Services.Shipping.Address;

/// <summary>
/// Service interface for managing user addresses
/// </summary>
public interface IAddressService
{
    /// <summary>
    /// Gets all addresses for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>List of addresses ordered by default flag and creation date</returns>
    Task<List<AddressEntity>> GetUserAddressesAsync(int userId);

    /// <summary>
    /// Gets a specific address by ID
    /// </summary>
    /// <param name="id">The address ID</param>
    /// <returns>Address or null if not found</returns>
    Task<AddressEntity?> GetAddressByIdAsync(int id);

    /// <summary>
    /// Gets the default address for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Default address or null if no default set</returns>
    Task<AddressEntity?> GetDefaultAddressAsync(int userId);

    /// <summary>
    /// Creates a new address
    /// </summary>
    /// <param name="address">The address to create</param>
    /// <returns>The created address with ID</returns>
    Task<AddressEntity> CreateAddressAsync(AddressEntity address);

    /// <summary>
    /// Updates an existing address
    /// </summary>
    /// <param name="address">The address to update</param>
    /// <returns>The updated address</returns>
    Task<AddressEntity> UpdateAddressAsync(AddressEntity address);

    /// <summary>
    /// Deletes an address
    /// </summary>
    /// <param name="id">The address ID to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAddressAsync(int id);

    /// <summary>
    /// Sets an address as the default for a user
    /// Automatically clears the default flag from other addresses
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="addressId">The address ID to set as default</param>
    /// <returns>True if successful</returns>
    Task<bool> SetDefaultAddressAsync(int userId, int addressId);
}
