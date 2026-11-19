using Identity.Internal.Domain;
using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Api;

/// <summary>
/// DataSeeder tự động nạp dữ liệu mẫu từ Demo vào PostgreSQL khi khởi động ở môi trường Development.
/// Nạp các tài khoản mẫu (Admin, Organizer, Customer), địa điểm (Venues), sự kiện (Events - OnSale)
/// và các hạng vé (TicketTypes) để test mua vé ngay lập tức.
/// </summary>
public static class DataSeeder
{
    private static readonly Guid CategoryMusicId = new("20000000-0000-0000-0000-000000000001");     // Âm nhạc
    private static readonly Guid CategoryTechId = new("20000000-0000-0000-0000-000000000002");      // Công nghệ
    private static readonly Guid CategorySportsId = new("20000000-0000-0000-0000-000000000003");    // Thể thao
    private static readonly Guid CategoryWorkshopId = new("20000000-0000-0000-0000-000000000004");  // Hội thảo & Workshop

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        try
        {
            logger.LogInformation("[DataSeeder] Bắt đầu kiểm tra và nạp dữ liệu mẫu...");

            var organizerId = await SeedUsersAsync(services, logger);
            await SeedEventsAndTicketsAsync(services, organizerId, logger);

            logger.LogInformation("[DataSeeder] Hoàn tất quá trình nạp dữ liệu mẫu.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DataSeeder] Lỗi khi nạp dữ liệu mẫu vào cơ sở dữ liệu.");
            throw;
        }
    }

    private static async Task<Guid> SeedUsersAsync(IServiceProvider services, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Admin
        const string adminEmail = "admin@ticketing.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            logger.LogInformation("[DataSeeder] Tạo tài khoản Admin: {Email}", adminEmail);
            admin = ApplicationUser.Create(adminEmail, "Admin Hệ Thống", "0901234567");
            var result = await userManager.CreateAsync(admin, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            else
            {
                logger.LogWarning("[DataSeeder] Không tạo được Admin: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // 2. Organizer
        const string organizerEmail = "organizer@ticketing.local";
        var organizer = await userManager.FindByEmailAsync(organizerEmail);
        if (organizer is null)
        {
            logger.LogInformation("[DataSeeder] Tạo tài khoản Organizer: {Email}", organizerEmail);
            organizer = ApplicationUser.Create(organizerEmail, "Ban Tổ Chức Sài Gòn", "0909999888");
            var result = await userManager.CreateAsync(organizer, "Organizer@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(organizer, "Organizer");
            }
            else
            {
                logger.LogWarning("[DataSeeder] Không tạo được Organizer: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // 3. Customer
        const string customerEmail = "customer@ticketing.local";
        var customer = await userManager.FindByEmailAsync(customerEmail);
        if (customer is null)
        {
            logger.LogInformation("[DataSeeder] Tạo tài khoản Customer: {Email}", customerEmail);
            customer = ApplicationUser.Create(customerEmail, "Khách Hàng Mẫu", "0908888777");
            var result = await userManager.CreateAsync(customer, "Customer@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(customer, "Customer");
            }
            else
            {
                logger.LogWarning("[DataSeeder] Không tạo được Customer: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // 4. Staff
        const string staffEmail = "staff@ticketing.local";
        var staff = await userManager.FindByEmailAsync(staffEmail);
        if (staff is null)
        {
            logger.LogInformation("[DataSeeder] Tạo tài khoản Staff: {Email}", staffEmail);
            staff = ApplicationUser.Create(staffEmail, "Nhân Viên Soát Vé", "0901112233");
            var result = await userManager.CreateAsync(staff, "Staff@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(staff, "Staff");
            }
            else
            {
                logger.LogWarning("[DataSeeder] Không tạo được Staff: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        return organizer!.Id;
    }

    private static async Task SeedEventsAndTicketsAsync(IServiceProvider services, Guid organizerId, ILogger logger)
    {
        var eventsDb = services.GetRequiredService<EventsDbContext>();
        var ticketingDb = services.GetRequiredService<TicketingDbContext>();

        // Idempotency: Nếu đã có sự kiện trong database thì bỏ qua
        if (await eventsDb.Events.AnyAsync())
        {
            logger.LogInformation("[DataSeeder] Bảng Events đã có dữ liệu. Bỏ qua nạp sự kiện mẫu.");
            return;
        }

        logger.LogInformation("[DataSeeder] Bắt đầu nạp 14 sự kiện và các hạng vé mẫu...");

        // 1. Tạo các địa điểm (Venues)
        var venues = new Dictionary<string, Venue>
        {
            ["global-city"] = Venue.Create("The Global City", "Khu đô thị The Global City, Đỗ Xuân Hợp, An Phú", "TP. Hồ Chí Minh", capacity: 5000),
            ["yen-so"] = Venue.Create("Công viên Yên Sở", "Gamuda City, Q. Hoàng Mai", "Hà Nội", capacity: 8000),
            ["sala-mall"] = Venue.Create("Thiso Mall Sala", "10 Mai Chí Thọ, Thủ Thiêm, TP. Thủ Đức", "TP. Hồ Chí Minh", capacity: 500),
            ["nha-gom"] = Venue.Create("Nhà của Gốm", "Bán đảo Sơn Trà", "Đà Nẵng", capacity: 50),
            ["bach-dang"] = Venue.Create("Công viên Bến Bạch Đằng", "Đường Tôn Đức Thắng, Bến Nghé, Quận 1", "TP. Hồ Chí Minh", capacity: 3000),
            ["ly-thai-to"] = Venue.Create("Vườn hoa Lý Thái Tổ", "Đinh Tiên Hoàng, Tràng Tiền, Hoàn Kiếm", "Hà Nội", capacity: 2000),
            ["vuon-bia"] = Venue.Create("Vườn Bia", "Bình Thạnh", "TP. Hồ Chí Minh", capacity: 300),
            ["con-coc"] = Venue.Create("Vườn hoa Con Cóc", "Phố Ngô Quyền, Hoàn Kiếm", "Hà Nội", capacity: 400),
            ["nia-coffee"] = Venue.Create("Nia Coffee", "Hải Châu", "Đà Nẵng", capacity: 100),
            ["complex-01"] = Venue.Create("Complex 01", "Tây Hồ", "Hà Nội", capacity: 350),
            ["my-khe"] = Venue.Create("Bãi biển Mỹ Khê", "Đường Võ Nguyên Giáp, Sơn Trà", "Đà Nẵng", capacity: 1500),
            ["factory-arts"] = Venue.Create("The Factory Contemporary Arts Centre", "15 Nguyễn Ư Dĩ, Thảo Điền, TP. Thủ Đức", "TP. Hồ Chí Minh", capacity: 600),
            ["apec-park"] = Venue.Create("Công viên APEC", "Bình Hiên, Hải Châu", "Đà Nẵng", capacity: 2500),
            ["opera-house"] = Venue.Create("Nhà hát Thành phố", "07 Công Trường Lam Sơn, Bến Nghé, Quận 1", "TP. Hồ Chí Minh", capacity: 800)
        };

        eventsDb.Venues.AddRange(venues.Values);
        await eventsDb.SaveChangesAsync();
        logger.LogInformation("[DataSeeder] Đã thêm {Count} địa điểm (Venues).", venues.Count);

        // 2. Định nghĩa danh sách 14 sự kiện và vé mẫu
        var eventDefinitions = GetEventSeedDefinitions();

        var eventsToInsert = new List<Event>();
        var ticketsToInsert = new List<TicketType>();

        foreach (var def in eventDefinitions)
        {
            var venue = venues[def.VenueKey];
            var startsAt = DateTime.UtcNow.AddDays(def.DaysFromNow).Date.Add(def.TimeOfDay);
            var endsAt = startsAt.Add(def.Duration);

            var eventItem = Event.Create(
                organizerId,
                def.CategoryId,
                venue.Id,
                def.Title,
                def.Slug,
                def.Description,
                startsAt,
                endsAt);

            // Chuyển sang trạng thái OnSale để hiển thị trên API công khai
            eventItem.SubmitForApproval();
            eventItem.Approve();

            var minPrice = def.Tiers.Min(t => t.Price);
            var maxPrice = def.Tiers.Max(t => t.Price);
            eventItem.UpdatePriceRange(minPrice, maxPrice);

            if (!string.IsNullOrEmpty(def.CoverImageKey))
            {
                eventItem.SetCoverImage(def.CoverImageKey);
            }

            eventsToInsert.Add(eventItem);

            // Tạo các hạng vé cho sự kiện
            foreach (var tier in def.Tiers)
            {
                var ticketType = TicketType.Create(
                    eventItem.Id,
                    tier.Name,
                    tier.Price,
                    tier.TotalQuantity,
                    tier.MaxPerOrder,
                    saleStartsAt: DateTime.UtcNow,
                    saleEndsAt: endsAt);

                ticketsToInsert.Add(ticketType);
            }
        }

        eventsDb.Events.AddRange(eventsToInsert);
        await eventsDb.SaveChangesAsync();
        logger.LogInformation("[DataSeeder] Đã lưu {Count} sự kiện (Status: OnSale).", eventsToInsert.Count);

        ticketingDb.TicketTypes.AddRange(ticketsToInsert);
        await ticketingDb.SaveChangesAsync();
        logger.LogInformation("[DataSeeder] Đã lưu {Count} hạng vé (TicketTypes) tương ứng.", ticketsToInsert.Count);
    }

    private static List<EventSeedDefinition> GetEventSeedDefinitions() =>
    [
        new(
            Title: "MIDNIGHT JAZZ — Chạm vào đêm",
            Slug: "midnight-jazz",
            CategoryId: CategoryMusicId,
            VenueKey: "global-city",
            DaysFromNow: 15,
            TimeOfDay: new TimeSpan(19, 30, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Một đêm nhạc jazz ngoài trời nơi những giai điệu ngẫu hứng gặp ánh đèn thành phố. Thưởng thức những bản phối mới cùng dàn nghệ sĩ độc lập trong không gian mở đầy cảm hứng.",
            CoverImageKey: "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1400&q=85",
            Tiers:
            [
                new("Standard", 590000m, 200, 5),
                new("Premium", 990000m, 100, 4),
                new("Backstage pass", 1590000m, 30, 2)
            ]
        ),
        new(
            Title: "The Sound of Spring",
            Slug: "the-sound-of-spring",
            CategoryId: CategoryMusicId,
            VenueKey: "yen-so",
            DaysFromNow: 20,
            TimeOfDay: new TimeSpan(16, 0, 0),
            Duration: TimeSpan.FromHours(4),
            Description: "Lễ hội âm nhạc chào mùa mới với những nghệ sĩ trẻ và không gian xanh.",
            CoverImageKey: "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Early bird", 450000m, 150, 5),
                new("Garden pass", 780000m, 50, 4)
            ]
        ),
        new(
            Title: "Creative Morning: Làm điều khác",
            Slug: "creative-morning",
            CategoryId: CategoryWorkshopId,
            VenueKey: "sala-mall",
            DaysFromNow: 7,
            TimeOfDay: new TimeSpan(8, 30, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Buổi trò chuyện thân mật về sáng tạo, nghề nghiệp và những ngã rẽ thú vị.",
            CoverImageKey: "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("General admission", 180000m, 100, 4),
                new("Supporter", 350000m, 30, 4)
            ]
        ),
        new(
            Title: "Chạm đất — Workshop gốm cuối tuần",
            Slug: "ceramic-sunday",
            CategoryId: CategoryWorkshopId,
            VenueKey: "nha-gom",
            DaysFromNow: 12,
            TimeOfDay: new TimeSpan(9, 0, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Tự tay tạo nên chiếc cốc của riêng bạn trong không gian studio nhỏ xinh.",
            CoverImageKey: "https://images.unsplash.com/photo-1565193566173-7a0ee3dbe261?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Workshop seat", 420000m, 25, 2)
            ]
        ),
        new(
            Title: "Saigon Street Food Night",
            Slug: "street-food-night",
            CategoryId: CategoryWorkshopId,
            VenueKey: "bach-dang",
            DaysFromNow: 18,
            TimeOfDay: new TimeSpan(17, 0, 0),
            Duration: TimeSpan.FromHours(5),
            Description: "Dạo một vòng Sài Gòn qua những món ăn đường phố được tuyển chọn.",
            CoverImageKey: "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Taster pass", 250000m, 200, 6),
                new("Foodie pass", 490000m, 100, 6)
            ]
        ),
        new(
            Title: "Run by the River 2026",
            Slug: "run-by-the-river",
            CategoryId: CategorySportsId,
            VenueKey: "ly-thai-to",
            DaysFromNow: 25,
            TimeOfDay: new TimeSpan(5, 30, 0),
            Duration: TimeSpan.FromHours(4),
            Description: "Giải chạy cộng đồng 5 km bên bờ hồ, khép lại năm bằng năng lượng tích cực.",
            CoverImageKey: "https://images.unsplash.com/photo-1530549387789-4c1017266635?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("5K Runner", 320000m, 300, 5),
                new("Family pack", 790000m, 50, 2)
            ]
        ),
        new(
            Title: "Saigon Indie Live — Những ngày xanh",
            Slug: "saigon-indie-live",
            CategoryId: CategoryMusicId,
            VenueKey: "vuon-bia",
            DaysFromNow: 16,
            TimeOfDay: new TimeSpan(18, 30, 0),
            Duration: TimeSpan.FromHours(4),
            Description: "Một đêm nhạc indie gần gũi với những giai điệu để hát cùng nhau dưới ánh đèn vàng.",
            CoverImageKey: "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("General admission", 290000m, 150, 5),
                new("Front stage", 550000m, 50, 4)
            ]
        ),
        new(
            Title: "Phim ngoài trời: Những thước phim cũ",
            Slug: "film-under-stars",
            CategoryId: CategoryWorkshopId,
            VenueKey: "con-coc",
            DaysFromNow: 10,
            TimeOfDay: new TimeSpan(19, 0, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Buổi chiếu phim ngoài trời ấm cúng dành cho những người yêu điện ảnh và thành phố.",
            CoverImageKey: "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Lawn seat", 150000m, 100, 4),
                new("Couple set", 260000m, 40, 2)
            ]
        ),
        new(
            Title: "Coffee Lab: Tự pha một ngày vui",
            Slug: "coffee-lab",
            CategoryId: CategoryWorkshopId,
            VenueKey: "nia-coffee",
            DaysFromNow: 11,
            TimeOfDay: new TimeSpan(9, 30, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Khám phá hạt cà phê địa phương và tự tay pha một tách theo gu của bạn.",
            CoverImageKey: "https://images.unsplash.com/photo-1442512595331-e89e73853f31?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Workshop seat", 280000m, 30, 2)
            ]
        ),
        new(
            Title: "Design for Good — Thiết kế điều tử tế",
            Slug: "design-for-good",
            CategoryId: CategoryTechId,
            VenueKey: "complex-01",
            DaysFromNow: 14,
            TimeOfDay: new TimeSpan(13, 30, 0),
            Duration: TimeSpan.FromHours(4),
            Description: "Gặp gỡ cộng đồng sáng tạo và cùng trò chuyện về thiết kế tạo tác động tích cực.",
            CoverImageKey: "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Regular", 220000m, 120, 5),
                new("Student", 120000m, 50, 2)
            ]
        ),
        new(
            Title: "Sunrise Yoga bên biển",
            Slug: "danang-sunrise-yoga",
            CategoryId: CategorySportsId,
            VenueKey: "my-khe",
            DaysFromNow: 22,
            TimeOfDay: new TimeSpan(5, 45, 0),
            Duration: TimeSpan.FromHours(2),
            Description: "Đón bình minh bên biển với một buổi yoga nhẹ nhàng, phù hợp cả người mới bắt đầu.",
            CoverImageKey: "https://images.unsplash.com/photo-1506126613408-eca07ce68773?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Open class", 190000m, 60, 3)
            ]
        ),
        new(
            Title: "Makers Market — Chợ phiên sáng tạo",
            Slug: "makers-market",
            CategoryId: CategoryTechId,
            VenueKey: "factory-arts",
            DaysFromNow: 28,
            TimeOfDay: new TimeSpan(10, 0, 0),
            Duration: TimeSpan.FromHours(8),
            Description: "Một ngày gặp gỡ những thương hiệu thủ công độc lập, nghệ thuật và ý tưởng mới.",
            CoverImageKey: "https://images.unsplash.com/photo-1531058020387-3be344556be6?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Day pass", 80000m, 300, 6),
                new("Workshop pass", 240000m, 50, 4)
            ]
        ),
        new(
            Title: "Hương vị miền Trung",
            Slug: "taste-of-central",
            CategoryId: CategoryWorkshopId,
            VenueKey: "apec-park",
            DaysFromNow: 35,
            TimeOfDay: new TimeSpan(16, 30, 0),
            Duration: TimeSpan.FromHours(5),
            Description: "Thưởng thức những món ngon miền Trung qua một hành trình ẩm thực bên sông Hàn.",
            CoverImageKey: "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Tasting pass", 199000m, 200, 5),
                new("Food lover", 399000m, 100, 5)
            ]
        ),
        new(
            Title: "City Night Ride — Đạp xe qua phố",
            Slug: "city-night-ride",
            CategoryId: CategorySportsId,
            VenueKey: "opera-house",
            DaysFromNow: 40,
            TimeOfDay: new TimeSpan(20, 0, 0),
            Duration: TimeSpan.FromHours(3),
            Description: "Cùng nhau đạp xe khám phá những góc phố quen khi thành phố lên đèn.",
            CoverImageKey: "https://images.unsplash.com/photo-1511994298241-608e28f14fde?auto=format&fit=crop&w=900&q=80",
            Tiers:
            [
                new("Tự mang xe", 120000m, 100, 4),
                new("Có xe đạp", 260000m, 30, 2)
            ]
        )
    ];

    private sealed record EventSeedDefinition(
        string Title,
        string Slug,
        Guid CategoryId,
        string VenueKey,
        int DaysFromNow,
        TimeSpan TimeOfDay,
        TimeSpan Duration,
        string Description,
        string CoverImageKey,
        List<TierDefinition> Tiers);

    private sealed record TierDefinition(
        string Name,
        decimal Price,
        int TotalQuantity,
        int MaxPerOrder);
}
