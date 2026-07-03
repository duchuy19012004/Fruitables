namespace Fruitables.Models
{
    /// <summary>
    /// Khu vực giao hàng
    /// </summary>
    public enum ShippingZone
    {
        Zone1_InnerCity = 1,    // Nội thành
        Zone2_OuterCity = 2,    // Ngoại thành
        Zone3_Remote = 3        // Vùng xa
    }

    /// <summary>
    /// Cấu hình phí vận chuyển
    /// </summary>
    public class ShippingConfig
    {
        /// <summary>
        /// Phí ship Nội thành (mặc định 30,000đ)
        /// </summary>
        public decimal FeeZone1 { get; set; } = 30000;

        /// <summary>
        /// Phí ship Ngoại thành (mặc định 40,000đ)
        /// </summary>
        public decimal FeeZone2 { get; set; } = 40000;

        /// <summary>
        /// Phí ship Vùng xa (mặc định 50,000đ)
        /// </summary>
        public decimal FeeZone3 { get; set; } = 50000;

        /// <summary>
        /// Ngưỡng miễn phí ship (mặc định 500,000đ)
        /// </summary>
        public decimal FreeShippingThreshold { get; set; } = 500000;

        /// <summary>
        /// Phí giảm cho Vùng xa khi đạt ngưỡng (mặc định 20,000đ)
        /// </summary>
        public decimal ReducedFeeZone3 { get; set; } = 20000;

        /// <summary>
        /// Danh sách quận/huyện thuộc Khu vực 1 (Nội thành)
        /// Mặc định: Q1, Q3, Q4, Q5, Q10, Q11, Bình Thạnh, Phú Nhuận, Tân Bình, Tân Phú, Gò Vấp
        /// </summary>
        public List<string> Zone1Districts { get; set; } = DefaultZone1Districts.ToList();

        /// <summary>
        /// Danh sách quận/huyện thuộc Khu vực 2 (Ngoại thành)
        /// Mặc định: Thủ Đức, Q7, Q8, Q12, Bình Tân, Nhà Bè, Hóc Môn
        /// </summary>
        public List<string> Zone2Districts { get; set; } = DefaultZone2Districts.ToList();

        /// <summary>
        /// Danh sách mặc định quận/huyện Khu vực 1 (Nội thành)
        /// Requirements 3.1: Cấu hình danh sách quận/huyện
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultZone1Districts = new List<string>
        {
            "Quận 1", "Quận 3", "Quận 4", "Quận 5", "Quận 10", "Quận 11",
            "Bình Thạnh", "Phú Nhuận", "Tân Bình", "Tân Phú", "Gò Vấp"
        };

        /// <summary>
        /// Danh sách mặc định quận/huyện Khu vực 2 (Ngoại thành)
        /// Requirements 3.1: Cấu hình danh sách quận/huyện
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultZone2Districts = new List<string>
        {
            "Thủ Đức", "Quận 7", "Quận 8", "Quận 12",
            "Bình Tân", "Nhà Bè", "Hóc Môn"
        };
    }

    /// <summary>
    /// Thông tin phí vận chuyển cho một đơn hàng
    /// </summary>
    public class ShippingInfo
    {
        /// <summary>
        /// Phí vận chuyển
        /// </summary>
        public decimal ShippingFee { get; set; }

        /// <summary>
        /// Khu vực giao hàng
        /// </summary>
        public ShippingZone Zone { get; set; }

        /// <summary>
        /// Có được miễn phí ship không
        /// </summary>
        public bool IsFreeShipping { get; set; }

        /// <summary>
        /// Có được giảm phí ship không (áp dụng cho Vùng xa)
        /// </summary>
        public bool IsReducedShipping { get; set; }

        /// <summary>
        /// Số tiền cần mua thêm để được miễn phí ship
        /// </summary>
        public decimal AmountToFreeShipping { get; set; }

        /// <summary>
        /// Thông báo hiển thị cho khách hàng
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    public sealed record ShippingPackage(int Weight, int Length, int Width, int Height)
    {
        public static ShippingPackage FromTotalKg(int totalKg)
        {
            var kg = Math.Max(0, totalKg);
            var weight = kg * 1000;

            return kg switch
            {
                <= 2 => new ShippingPackage(weight, 20, 15, 10),
                <= 5 => new ShippingPackage(weight, 30, 20, 15),
                _ => new ShippingPackage(weight, 40, 30, 20)
            };
        }
    }
}
