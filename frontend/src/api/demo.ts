import { EVENT_PAGE_SIZE, type CheckoutInput, type EventItem, type PurchasedTicket, type Reservation } from "./types";

const events: EventItem[] = [
  {
    id: "midnight-jazz",
    title: "MIDNIGHT JAZZ — Chạm vào đêm",
    category: "Âm nhạc",
    location: "TP. Hồ Chí Minh",
    venue: "The Global City, TP. Thủ Đức",
    date: "2026-12-12",
    time: "19:30",
    image: "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1400&q=85",
    accent: "#173e42",
    description: "Một đêm nhạc jazz ngoài trời nơi những giai điệu ngẫu hứng gặp ánh đèn thành phố. Thưởng thức những bản phối mới cùng dàn nghệ sĩ độc lập trong không gian mở đầy cảm hứng.",
    organizer: "Nights & Notes",
    featured: true,
    tiers: [
      { id: "standard", name: "Standard", price: 590000, remaining: 126, description: "Khu vực đứng, tầm nhìn sân khấu" },
      { id: "premium", name: "Premium", price: 990000, remaining: 42, description: "Khu vực gần sân khấu, 1 đồ uống" },
      { id: "vip", name: "Backstage pass", price: 1590000, remaining: 18, description: "Lối vào riêng, gặp gỡ nghệ sĩ" },
    ],
  },
  {
    id: "the-sound-of-spring",
    title: "The Sound of Spring",
    category: "Âm nhạc",
    location: "Hà Nội",
    venue: "Công viên Yên Sở, Hoàng Mai",
    date: "2026-12-21",
    time: "16:00",
    image: "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=900&q=80",
    accent: "#dba77d",
    description: "Lễ hội âm nhạc chào mùa mới với những nghệ sĩ trẻ và không gian xanh.",
    organizer: "Spring Studio",
    tiers: [
      { id: "early", name: "Early bird", price: 450000, remaining: 89, description: "Vé vào cổng tiêu chuẩn" },
      { id: "garden", name: "Garden pass", price: 780000, remaining: 32, description: "Khu vực lounge và đồ uống" },
    ],
  },
  {
    id: "creative-morning",
    title: "Creative Morning: Làm điều khác",
    category: "Hội thảo",
    location: "TP. Hồ Chí Minh",
    venue: "Thiso Mall Sala, TP. Thủ Đức",
    date: "2026-11-29",
    time: "08:30",
    image: "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=900&q=80",
    accent: "#e9c9ad",
    description: "Buổi trò chuyện thân mật về sáng tạo, nghề nghiệp và những ngã rẽ thú vị.",
    organizer: "Creative Mornings",
    tiers: [
      { id: "general", name: "General admission", price: 180000, remaining: 47, description: "Vào cửa và cà phê" },
      { id: "supporter", name: "Supporter", price: 350000, remaining: 19, description: "Chỗ ngồi ưu tiên và quà tặng" },
    ],
  },
  {
    id: "ceramic-sunday",
    title: "Chạm đất — Workshop gốm cuối tuần",
    category: "Nghệ thuật",
    location: "Đà Nẵng",
    venue: "Nhà của Gốm, Sơn Trà",
    date: "2026-12-07",
    time: "09:00",
    image: "https://images.unsplash.com/photo-1565193566173-7a0ee3dbe261?auto=format&fit=crop&w=900&q=80",
    accent: "#dcb39d",
    description: "Tự tay tạo nên chiếc cốc của riêng bạn trong không gian studio nhỏ xinh.",
    organizer: "Nhà của Gốm",
    tiers: [
      { id: "workshop", name: "Workshop seat", price: 420000, remaining: 12, description: "Gồm đất sét, nung và trà" },
    ],
  },
  {
    id: "street-food-night",
    title: "Saigon Street Food Night",
    category: "Ẩm thực",
    location: "TP. Hồ Chí Minh",
    venue: "Bến Bạch Đằng, Quận 1",
    date: "2026-12-14",
    time: "17:00",
    image: "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?auto=format&fit=crop&w=900&q=80",
    accent: "#d3b786",
    description: "Dạo một vòng Sài Gòn qua những món ăn đường phố được tuyển chọn.",
    organizer: "Saigon Local",
    tiers: [
      { id: "taster", name: "Taster pass", price: 250000, remaining: 104, description: "Phiếu trải nghiệm ẩm thực" },
      { id: "foodie", name: "Foodie pass", price: 490000, remaining: 61, description: "Phiếu ăn uống và nước" },
    ],
  },
  {
    id: "run-by-the-river",
    title: "Run by the River 2026",
    category: "Thể thao",
    location: "Hà Nội",
    venue: "Vườn hoa Lý Thái Tổ, Hoàn Kiếm",
    date: "2026-12-28",
    time: "05:30",
    image: "https://images.unsplash.com/photo-1530549387789-4c1017266635?auto=format&fit=crop&w=900&q=80",
    accent: "#acc7bd",
    description: "Giải chạy cộng đồng 5 km bên bờ hồ, khép lại năm bằng năng lượng tích cực.",
    organizer: "Run Together",
    tiers: [
      { id: "five-k", name: "5K Runner", price: 320000, remaining: 140, description: "Bộ race-kit và huy chương" },
      { id: "family", name: "Family pack", price: 790000, remaining: 24, description: "3 race-kit, tiết kiệm hơn" },
    ],
  },
  {
    id: "saigon-indie-live",
    title: "Saigon Indie Live — Những ngày xanh",
    category: "Âm nhạc",
    location: "TP. Hồ Chí Minh",
    venue: "Vườn Bia, Bình Thạnh",
    date: "2026-12-19",
    time: "18:30",
    image: "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?auto=format&fit=crop&w=900&q=80",
    accent: "#b7d0bc",
    description: "Một đêm nhạc indie gần gũi với những giai điệu để hát cùng nhau dưới ánh đèn vàng.",
    organizer: "Saigon Indie Club",
    tiers: [
      { id: "indie-ga", name: "General admission", price: 290000, remaining: 96, description: "Vé vào cổng tiêu chuẩn" },
      { id: "indie-front", name: "Front stage", price: 550000, remaining: 28, description: "Khu vực gần sân khấu, gồm đồ uống" },
    ],
  },
  {
    id: "film-under-stars",
    title: "Phim ngoài trời: Những thước phim cũ",
    category: "Nghệ thuật",
    location: "Hà Nội",
    venue: "Vườn hoa Con Cóc, Hoàn Kiếm",
    date: "2026-12-05",
    time: "19:00",
    image: "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=900&q=80",
    accent: "#d8c6b5",
    description: "Buổi chiếu phim ngoài trời ấm cúng dành cho những người yêu điện ảnh và thành phố.",
    organizer: "Màn Ảnh Nhỏ",
    tiers: [
      { id: "film-seat", name: "Lawn seat", price: 150000, remaining: 73, description: "Chỗ ngồi cỏ và bắp rang" },
      { id: "film-date", name: "Couple set", price: 260000, remaining: 20, description: "Hai chỗ ngồi và hai phần đồ ăn" },
    ],
  },
  {
    id: "coffee-lab",
    title: "Coffee Lab: Tự pha một ngày vui",
    category: "Ẩm thực",
    location: "Đà Nẵng",
    venue: "Nia Coffee, Hải Châu",
    date: "2026-12-06",
    time: "09:30",
    image: "https://images.unsplash.com/photo-1442512595331-e89e73853f31?auto=format&fit=crop&w=900&q=80",
    accent: "#d9c1a8",
    description: "Khám phá hạt cà phê địa phương và tự tay pha một tách theo gu của bạn.",
    organizer: "Nia Coffee",
    tiers: [
      { id: "coffee-seat", name: "Workshop seat", price: 280000, remaining: 16, description: "Nếm thử 3 loại cà phê" },
    ],
  },
  {
    id: "design-for-good",
    title: "Design for Good — Thiết kế điều tử tế",
    category: "Hội thảo",
    location: "Hà Nội",
    venue: "Complex 01, Tây Hồ",
    date: "2026-12-13",
    time: "13:30",
    image: "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80",
    accent: "#c5c6e2",
    description: "Gặp gỡ cộng đồng sáng tạo và cùng trò chuyện về thiết kế tạo tác động tích cực.",
    organizer: "Design Buddies",
    tiers: [
      { id: "design-regular", name: "Regular", price: 220000, remaining: 58, description: "Tham dự toàn bộ chương trình" },
      { id: "design-student", name: "Student", price: 120000, remaining: 22, description: "Vé ưu đãi dành cho sinh viên" },
    ],
  },
  {
    id: "danang-sunrise-yoga",
    title: "Sunrise Yoga bên biển",
    category: "Thể thao",
    location: "Đà Nẵng",
    venue: "Bãi biển Mỹ Khê, Sơn Trà",
    date: "2026-12-20",
    time: "05:45",
    image: "https://images.unsplash.com/photo-1506126613408-eca07ce68773?auto=format&fit=crop&w=900&q=80",
    accent: "#c6ddd9",
    description: "Đón bình minh bên biển với một buổi yoga nhẹ nhàng, phù hợp cả người mới bắt đầu.",
    organizer: "Breathe Danang",
    tiers: [
      { id: "yoga-class", name: "Open class", price: 190000, remaining: 34, description: "Thảm tập và nước trái cây" },
    ],
  },
  {
    id: "makers-market",
    title: "Makers Market — Chợ phiên sáng tạo",
    category: "Nghệ thuật",
    location: "TP. Hồ Chí Minh",
    venue: "The Factory Contemporary Arts Centre, Thủ Đức",
    date: "2026-12-26",
    time: "10:00",
    image: "https://images.unsplash.com/photo-1531058020387-3be344556be6?auto=format&fit=crop&w=900&q=80",
    accent: "#edd5b8",
    description: "Một ngày gặp gỡ những thương hiệu thủ công độc lập, nghệ thuật và ý tưởng mới.",
    organizer: "Makers Vietnam",
    tiers: [
      { id: "makers-entry", name: "Day pass", price: 80000, remaining: 210, description: "Vào cửa tự do các gian hàng" },
      { id: "makers-workshop", name: "Workshop pass", price: 240000, remaining: 31, description: "Vé vào cửa và một workshop" },
    ],
  },
  {
    id: "taste-of-central",
    title: "Hương vị miền Trung",
    category: "Ẩm thực",
    location: "Đà Nẵng",
    venue: "Công viên APEC, Hải Châu",
    date: "2027-01-09",
    time: "16:30",
    image: "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80",
    accent: "#e5c9ad",
    description: "Thưởng thức những món ngon miền Trung qua một hành trình ẩm thực bên sông Hàn.",
    organizer: "Bếp Nhà Mình",
    tiers: [
      { id: "taste-mini", name: "Tasting pass", price: 199000, remaining: 110, description: "Phiếu thử 5 món đặc sản" },
      { id: "taste-full", name: "Food lover", price: 399000, remaining: 64, description: "Phiếu thử món và đồ uống" },
    ],
  },
  {
    id: "city-night-ride",
    title: "City Night Ride — Đạp xe qua phố",
    category: "Thể thao",
    location: "TP. Hồ Chí Minh",
    venue: "Nhà hát Thành phố, Quận 1",
    date: "2027-01-16",
    time: "20:00",
    image: "https://images.unsplash.com/photo-1511994298241-608e28f14fde?auto=format&fit=crop&w=900&q=80",
    accent: "#bdd0d6",
    description: "Cùng nhau đạp xe khám phá những góc phố quen khi thành phố lên đèn.",
    organizer: "Saigon Cycle Club",
    tiers: [
      { id: "ride-own-bike", name: "Tự mang xe", price: 120000, remaining: 48, description: "Hướng dẫn viên và nước uống" },
      { id: "ride-rental", name: "Có xe đạp", price: 260000, remaining: 15, description: "Bao gồm thuê xe và đèn an toàn" },
    ],
  },
];

const additionalEvents: EventItem[] = [
  demoEvent("saigon-soul-session", "Saigon Soul Session", "Âm nhạc", "TP. Hồ Chí Minh", "Soul Live Project, Quận 3", "2026-12-27", "19:00", "photo-1470229722913-7c0e2dbbafd3", "Đêm nhạc soul ấm áp với những giọng ca đầy nội lực và ban nhạc chơi live.", "Soul Live", 390000),
  demoEvent("acoustic-rooftop", "Acoustic trên tầng mây", "Âm nhạc", "Hà Nội", "Trill Rooftop Cafe, Thanh Xuân", "2027-01-03", "19:30", "photo-1516280440614-37939bbacd81", "Nghe những bản tình ca quen thuộc trong không gian rooftop nhìn xuống thành phố.", "Trill Music", 220000),
  demoEvent("danang-beach-festival", "Sóng — Lễ hội âm nhạc biển", "Âm nhạc", "Đà Nẵng", "Công viên Biển Đông, Sơn Trà", "2027-01-10", "17:00", "photo-1506157786151-b8491531f063", "Sân khấu bên biển, những ban nhạc trẻ và một buổi chiều ngập nắng.", "Sóng Festival", 490000),
  demoEvent("vinyl-listening-club", "Vinyl Listening Club: Side A / Side B", "Âm nhạc", "TP. Hồ Chí Minh", "The Lighthouse, Quận 1", "2027-01-17", "20:00", "photo-1461360228754-6e81c478b882", "Cùng nghe những album kinh điển trên đĩa than và chia sẻ câu chuyện phía sau từng bài hát.", "Vinyl Listening Club", 180000),
  demoEvent("han-river-symphony", "Giao hưởng bên sông Hàn", "Âm nhạc", "Đà Nẵng", "Công viên APEC, Hải Châu", "2027-01-24", "19:00", "photo-1507838153414-b4b713384a76", "Đêm nhạc giao hưởng mở với những giai điệu quen thuộc vang lên bên bờ sông.", "Danang Philharmonic", 350000),
  demoEvent("ha-noi-hiphop-jam", "Hà Nội Hip-hop Jam", "Âm nhạc", "Hà Nội", "Complex 01, Tây Hồ", "2027-01-31", "18:00", "photo-1493225457124-a3eb161ffa5f", "Một cuộc hẹn của rap, breaking và văn hóa đường phố cùng các nghệ sĩ độc lập.", "Northside Collective", 280000),
  demoEvent("saigon-music-week", "Saigon Music Week — Thành phố lên đèn", "Âm nhạc", "TP. Hồ Chí Minh", "Nhà văn hóa Thanh Niên, Quận 1", "2027-02-07", "16:30", "photo-1459749411175-04bf5292ceea", "Sân khấu đa thể loại quy tụ nghệ sĩ mới và những gương mặt được yêu thích.", "Saigon Music Week", 520000),

  demoEvent("watercolor-and-wine", "Màu nước & một chút vang", "Nghệ thuật", "TP. Hồ Chí Minh", "The Hive Villa, Thảo Điền", "2026-12-09", "18:30", "photo-1513364776144-60967b0f800f", "Thử vẽ màu nước trong buổi tối thư thả, có hướng dẫn và một ly vang nhẹ.", "Paper & Paint", 390000),
  demoEvent("photo-walk-hanoi", "Photo Walk: Hà Nội qua ô cửa", "Nghệ thuật", "Hà Nội", "Nhà hát Lớn Hà Nội, Hoàn Kiếm", "2026-12-16", "07:00", "photo-1452587925148-ce544e77e70d", "Đi bộ chụp ảnh phố cổ cùng nhiếp ảnh gia địa phương, dành cho mọi loại máy ảnh.", "Hanoi Photo Walk", 160000),
  demoEvent("clay-and-tea", "Nặn đất, uống trà", "Nghệ thuật", "Đà Nẵng", "Gốm Nhà Mình, Ngũ Hành Sơn", "2026-12-23", "14:00", "photo-1578749556568-bc2c40e68b61", "Một buổi chiều chạm tay vào đất sét, tạo hình món đồ nhỏ của riêng bạn.", "Gốm Nhà Mình", 320000),
  demoEvent("independent-art-night", "Independent Art Night", "Nghệ thuật", "TP. Hồ Chí Minh", "The Factory Contemporary Arts Centre, Thủ Đức", "2026-12-30", "18:00", "photo-1579783902614-a3fb3927b6a5", "Gặp gỡ nghệ sĩ trẻ, xem triển lãm pop-up và trò chuyện về nghệ thuật đương đại.", "The Factory", 140000),
  demoEvent("printmaking-weekend", "Cuối tuần in tranh thủ công", "Nghệ thuật", "Hà Nội", "Toong Tràng Thi, Hoàn Kiếm", "2027-01-06", "09:30", "photo-1455390582262-044cdead277a", "Làm quen với kỹ thuật in khắc đơn giản và mang tác phẩm của bạn về nhà.", "Xưởng In Nhỏ", 290000),
  demoEvent("danang-art-film", "Art Film Sunday: Thành phố và ký ức", "Nghệ thuật", "Đà Nẵng", "Wanderlust Coffee, Hải Châu", "2027-01-13", "15:00", "photo-1478720568477-152d9b164e26", "Buổi chiếu phim nghệ thuật ngắn và trò chuyện mở sau khi xem.", "Màn Ảnh Độc Lập", 120000),
  demoEvent("botanical-illustration", "Vẽ cây lá — Botanical Illustration", "Nghệ thuật", "TP. Hồ Chí Minh", "Saigon Outcast, Thảo Điền", "2027-01-27", "09:00", "photo-1513364776144-60967b0f800f", "Quan sát lá cây, phối màu và hoàn thiện một bức minh họa thực vật của riêng mình.", "Little Fern Studio", 350000),

  demoEvent("product-people-summit", "Product & People: Làm sản phẩm có ích", "Hội thảo", "TP. Hồ Chí Minh", "Dreamplex, Quận 1", "2026-12-04", "09:00", "photo-1540575467063-178a50c2df87", "Các nhóm sản phẩm chia sẻ cách lắng nghe người dùng và xây dựng trải nghiệm tốt hơn.", "Product People Vietnam", 450000),
  demoEvent("founders-coffee", "Founders' Coffee — Từ ý tưởng đến ngày đầu tiên", "Hội thảo", "Hà Nội", "The Sentry, Ba Đình", "2026-12-11", "08:30", "photo-1517248135467-4c7edcad34c4", "Buổi gặp gỡ gần gũi cho người đang bắt đầu dự án và muốn học từ trải nghiệm thật.", "Founders' Coffee", 170000),
  demoEvent("climate-action-lab", "Climate Action Lab: Bắt đầu từ khu phố", "Hội thảo", "Đà Nẵng", "Enouvo Space, Hải Châu", "2026-12-18", "13:30", "photo-1517486808906-6ca8b3f04846", "Cùng cộng đồng địa phương tìm những hành động nhỏ giúp thành phố xanh hơn.", "Green Steps", 90000),
  demoEvent("personal-finance-cafe", "Tiền bạc không khó: Cà phê tài chính cá nhân", "Hội thảo", "TP. Hồ Chí Minh", "The Workshop Coffee, Quận 1", "2026-12-25", "09:00", "photo-1516321318423-f06f85e504b3", "Trò chuyện dễ hiểu về ngân sách, tiết kiệm và những quyết định tài chính đầu đời.", "Money Talk Vietnam", 130000),
  demoEvent("creative-careers-hanoi", "Creative Careers: Nghề sáng tạo ngoài đời thật", "Hội thảo", "Hà Nội", "VICAS Art Studio, Đống Đa", "2027-01-01", "14:00", "photo-1519389950473-47ba0277781c", "Người làm sáng tạo chia sẻ về portfolio, tìm khách hàng và phát triển sự nghiệp bền vững.", "Creative Careers VN", 210000),
  demoEvent("ai-for-everyday-work", "AI cho công việc hằng ngày", "Hội thảo", "TP. Hồ Chí Minh", "Innovation Hub, Quận 7", "2027-01-08", "09:30", "photo-1531482615713-2afd69097998", "Thực hành cách dùng công cụ AI có trách nhiệm để tiết kiệm thời gian cho công việc.", "Tech For All", 260000),
  demoEvent("danang-storytelling", "Storytelling cho thương hiệu địa phương", "Hội thảo", "Đà Nẵng", "DNC Coworking, Hải Châu", "2027-01-15", "14:00", "photo-1552664730-d307ca884978", "Học cách kể câu chuyện chân thật giúp sản phẩm địa phương kết nối với khách hàng.", "Brand Camp", 190000),
  demoEvent("women-in-tech-meetup", "Women in Tech — Kết nối và tiến bước", "Hội thảo", "Hà Nội", "CIC Tower, Cầu Giấy", "2027-01-22", "18:30", "photo-1521737711867-e3b97375f902", "Gặp gỡ những phụ nữ đang xây dựng sự nghiệp công nghệ và cùng trao đổi kinh nghiệm.", "Women Techmakers Hanoi", 100000),

  demoEvent("banh-mi-and-beyond", "Bánh mì & chuyện phố", "Ẩm thực", "TP. Hồ Chí Minh", "Nhà Văn hóa Thanh niên, Quận 1", "2026-12-03", "16:00", "photo-1553909489-cd47e0ef937f", "Nếm thử nhiều phiên bản bánh mì Sài Gòn và nghe những câu chuyện phía sau tiệm nhỏ.", "Saigon Local", 220000),
  demoEvent("hanoi-pho-class", "Một sáng học nấu phở Hà Nội", "Ẩm thực", "Hà Nội", "Bếp Của Mẹ, Ba Đình", "2026-12-10", "08:00", "photo-1547592180-85f173990554", "Tìm hiểu nước dùng và tự tay chuẩn bị một tô phở trong lớp nấu ăn thân mật.", "Bếp Của Mẹ", 390000),
  demoEvent("seafood-by-the-beach", "Hải sản chiều bên biển Mỹ Khê", "Ẩm thực", "Đà Nẵng", "Làng Cá, Sơn Trà", "2026-12-17", "17:30", "photo-1559737558-2f5a35f4523b", "Thưởng thức hải sản địa phương trong bữa tiệc nhỏ với gió biển và nhạc acoustic.", "Làng Cá", 480000),
  demoEvent("chocolate-tasting", "Chocolate Tasting: Từ hạt ca cao Việt", "Ẩm thực", "TP. Hồ Chí Minh", "Maison Marou, Quận 1", "2026-12-24", "15:00", "photo-1511381939415-e44015466834", "Nếm thử chocolate bean-to-bar và khám phá hương vị ca cao từ nhiều vùng trồng.", "Marou Chocolate", 260000),
  demoEvent("bun-cha-street-food", "Bún chả & những quán quen", "Ẩm thực", "Hà Nội", "Phố Nguyễn Hữu Huân, Hoàn Kiếm", "2026-12-31", "11:00", "photo-1555939594-58d7cb561ad1", "Theo chân người bản địa khám phá những hàng bún chả lâu năm trong phố cổ.", "Hanoi Food Walk", 240000),
  demoEvent("vegan-table", "Bàn tiệc xanh — Vegan Sunday", "Ẩm thực", "Đà Nẵng", "Roots Plant-based Cafe, Sơn Trà", "2027-01-07", "11:30", "photo-1512621776951-a57141f2eefd", "Bữa trưa nhiều màu sắc với nguyên liệu thực vật theo mùa và công thức sáng tạo.", "Roots Kitchen", 310000),
  demoEvent("tea-ritual-workshop", "Trà Việt: Một buổi nếm trà chậm", "Ẩm thực", "Hà Nội", "Trà Thảnh Thơi, Tây Hồ", "2027-01-14", "09:30", "photo-1544787219-7f47ccb76574", "Thử các dòng trà Việt và tìm hiểu cách pha trà để thưởng thức trọn vẹn hương vị.", "Trà Thảnh Thơi", 270000),

  demoEvent("pickleball-social", "Pickleball Social — Chơi vui cuối tuần", "Thể thao", "TP. Hồ Chí Minh", "Sân Pickleball Sala, TP. Thủ Đức", "2026-12-02", "08:00", "photo-1531415074968-036ba1b575da", "Làm quen với pickleball qua những trận đấu vui vẻ, có hướng dẫn cho người mới.", "Play Together", 180000),
  demoEvent("hanoi-lake-run", "Hồ Tây 5K — Chạy cùng bình minh", "Thể thao", "Hà Nội", "Phủ Tây Hồ, Tây Hồ", "2026-12-09", "05:30", "photo-1552674605-db6ffd4facb5", "Cung đường chạy 5 km bên hồ với nhóm pacer và bữa sáng nhẹ sau khi về đích.", "Hanoi Runners", 240000),
  demoEvent("danang-sup-day", "SUP Day: Chèo ván ngắm bán đảo", "Thể thao", "Đà Nẵng", "Bãi biển Thọ Quang, Sơn Trà", "2026-12-16", "06:00", "photo-1502680390469-be75c86b636f", "Thử chèo SUP an toàn cùng huấn luyện viên và ngắm bình minh trên biển.", "Blue Wave Club", 420000),
  demoEvent("saigon-bouldering", "Bouldering thử sức — Leo núi trong nhà", "Thể thao", "TP. Hồ Chí Minh", "Push Climbing, Bình Thạnh", "2026-12-23", "09:00", "photo-1522163182402-834f871fd851", "Trải nghiệm leo tường trong nhà với bài hướng dẫn cơ bản và thiết bị an toàn.", "Push Climbing", 250000),
  demoEvent("hanoi-badminton-open", "Badminton Open Play — Cầu lông kết bạn", "Thể thao", "Hà Nội", "Cầu Giấy Arena, Cầu Giấy", "2026-12-30", "18:00", "photo-1626224583764-f87db24ac4ea", "Các trận cầu lông giao lưu chia bảng, phù hợp nhiều trình độ và người chơi mới.", "Hanoi Open Play", 150000),
  demoEvent("danang-trail-intro", "Trail Intro — Đường mòn Sơn Trà", "Thể thao", "Đà Nẵng", "Bán đảo Sơn Trà, Sơn Trà", "2027-01-06", "05:15", "photo-1551632811-561732d1e306", "Làm quen chạy trail qua cung đường nhẹ, hướng dẫn kỹ thuật và chuẩn bị an toàn.", "Trail Friends", 290000),
  demoEvent("saigon-basketball-night", "Basketball Night — Ném bóng vào rổ", "Thể thao", "TP. Hồ Chí Minh", "Nhà thi đấu Rạch Miễu, Phú Nhuận", "2027-01-13", "19:00", "photo-1546519638-68e109498ffc", "Một buổi bóng rổ giao lưu có mini game ném rổ và trận đấu cộng đồng.", "Saigon Hoopers", 170000),
];

function demoEvent(
  id: string,
  title: string,
  category: EventItem["category"],
  location: EventItem["location"],
  venue: string,
  date: string,
  time: string,
  imageId: string,
  description: string,
  organizer: string,
  price: number,
): EventItem {
  const accents: Record<EventItem["category"], string> = {
    "Âm nhạc": "#b7d0bc",
    "Nghệ thuật": "#edd5b8",
    "Hội thảo": "#c5c6e2",
    "Ẩm thực": "#e5c9ad",
    "Thể thao": "#bdd0d6",
  };
  return {
    id,
    title,
    category,
    location,
    venue,
    date,
    time,
    image: `https://images.unsplash.com/${imageId}?auto=format&fit=crop&w=900&q=80`,
    accent: accents[category],
    description,
    organizer,
    tiers: [{
      id: `${id}-general`,
      name: "Vé tham dự",
      price,
      remaining: 24 + (id.length * 7) % 80,
      description: "Vào cửa và tham gia chương trình",
    }],
  };
}

events.push(...additionalEvents);

const reservationKey = "ticketing.demo.reservation";
const ticketsKey = "ticketing.demo.tickets";
const countKey = "ticketing.demo.checkinCount";

export const demoApi = {
  async events(filters: { query?: string; category?: string; location?: string; maxPrice?: number; page?: number }) {
    await delay(120);
    const query = filters.query?.trim().toLocaleLowerCase("vi");
    const filtered = events.filter((event) => {
      const minimumPrice = Math.min(...event.tiers.map((tier) => tier.price));
      return (!query || `${event.title} ${event.venue} ${event.organizer}`.toLocaleLowerCase("vi").includes(query))
        && (!filters.category || event.category === filters.category)
        && (!filters.location || event.location === filters.location)
        && (!filters.maxPrice || minimumPrice <= filters.maxPrice);
    });
    const page = filters.page ?? 1;
    return {
      items: filtered.slice((page - 1) * EVENT_PAGE_SIZE, page * EVENT_PAGE_SIZE),
      total: filtered.length,
    };
  },
  async event(id: string) {
    await delay(100);
    const event = events.find((item) => item.id === id);
    if (!event) throw new Error("Không tìm thấy sự kiện.");
    return event;
  },
  async reserve(eventId: string, tierId: string, quantity: number): Promise<Reservation> {
    const event = await this.event(eventId);
    const tier = event.tiers.find((item) => item.id === tierId);
    if (!tier || quantity < 1 || quantity > Math.min(6, tier.remaining)) throw new Error("Số lượng vé không hợp lệ.");
    const reservation = { id: crypto.randomUUID(), eventId, tierId, quantity, expiresAt: Date.now() + 10 * 60_000 };
    localStorage.setItem(reservationKey, JSON.stringify(reservation));
    return reservation;
  },
  async checkout(input: CheckoutInput) {
    const reservation = JSON.parse(localStorage.getItem(reservationKey) ?? "null") as Reservation | null;
    if (!reservation || reservation.id !== input.reservationId || reservation.expiresAt <= Date.now()) {
      throw new Error("Thời gian giữ vé đã hết. Vui lòng chọn vé lại.");
    }
    const event = await this.event(reservation.eventId);
    const tier = event.tiers.find((item) => item.id === reservation.tierId)!;
    const existing = JSON.parse(localStorage.getItem(ticketsKey) ?? "[]") as PurchasedTicket[];
    const newTickets = Array.from({ length: reservation.quantity }, () => ({
      id: crypto.randomUUID(),
      eventId: event.id,
      eventTitle: event.title,
      image: event.image,
      venue: event.venue,
      date: event.date,
      tierName: tier.name,
      holderName: input.name,
      code: `DEMO-${crypto.randomUUID().slice(0, 8).toUpperCase()}`,
      status: "valid" as const,
    }));
    localStorage.setItem(ticketsKey, JSON.stringify([...existing, ...newTickets]));
    localStorage.removeItem(reservationKey);
    return { paymentUrl: `/payment/return?status=success&order=${newTickets[0].id}` };
  },
  async tickets() {
    await delay(100);
    return JSON.parse(localStorage.getItem(ticketsKey) ?? "[]") as PurchasedTicket[];
  },
  async checkIn(code: string) {
    const tickets = JSON.parse(localStorage.getItem(ticketsKey) ?? "[]") as PurchasedTicket[];
    const index = tickets.findIndex((ticket) => ticket.code === code);
    if (index < 0) return { status: "invalid" as const, count: this.getCheckInCount() };
    if (tickets[index].status === "used") return { status: "used" as const, count: this.getCheckInCount() };
    if (tickets[index].status === "cancelled") return { status: "cancelled" as const, count: this.getCheckInCount() };
    tickets[index] = { ...tickets[index], status: "used" };
    localStorage.setItem(ticketsKey, JSON.stringify(tickets));
    localStorage.setItem(countKey, String(this.getCheckInCount() + 1));
    return { status: "valid" as const, count: this.getCheckInCount() };
  },
  async checkInCount() {
    return this.getCheckInCount();
  },
  getCheckInCount() {
    return Number(localStorage.getItem(countKey) ?? 128);
  },
};

function delay(ms: number) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}
