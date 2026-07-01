
/*
 * VERSA DuKhach All Views UI i18n
 * Patch goal: translate hard-coded UI text across Areas/DuKhach/Views.
 * Dynamic data from DB (POI names, tour descriptions, review comments) must come from DB translations.
 */
(function () {
    'use strict';

    const normalizeLang = value => String(value || 'vi').trim().toLowerCase().split('-')[0];

    function getCookie(name) {
        const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        const match = document.cookie.match(new RegExp('(?:^|; )' + escaped + '=([^;]*)'));
        return match ? decodeURIComponent(match[1]) : '';
    }

    const urlLang = new URL(window.location.href).searchParams.get('lang');
    const lang = normalizeLang(
        window.VERSA_DUKHACH_LANG ||
        urlLang ||
        getCookie('versa.dukhach.lang') ||
        localStorage.getItem('versa-dukhach-lang') ||
        'vi'
    );

    const keyDictionaries = {
        "vi": {
                "navHome": "Trang chủ",
                "navMap": "Bản đồ",
                "navScanQr": "Quét QR",
                "navTour": "Tour",
                "navRanking": "Xếp hạng",
                "navPlans": "Gói dịch vụ",
                "navOrders": "Đơn hàng",
                "navAccount": "Tài khoản",
                "navLogout": "Đăng xuất",
                "navLogin": "Đăng nhập",
                "navRegister": "Đăng ký",
                "footerText": "Bản đồ tham quan, tài khoản du khách và trải nghiệm dùng chung với app mobile.",
                "homeEyebrow": "Web cho du khách",
                "homeHeroTitlePrefix": "Khám phá thành phố bằng",
                "homeHeroTitleHighlight": "bản đồ thông minh.",
                "homeHeroDesc": "Du khách có thể đăng ký, đăng nhập, xem bản đồ POI, check-in bằng vị trí và dùng cùng một tài khoản trên app mobile.",
                "homeOpenMap": "Mở bản đồ",
                "homeTourList": "Danh sách Tour",
                "homeRanking": "Bảng xếp hạng",
                "homeCreateAccount": "Tạo tài khoản du khách",
                "homeMyProfile": "Hồ sơ của tôi",
                "homePhoneTitle": "Bản đồ POI gần bạn",
                "homePhoneDesc": "Xem vị trí, bán kính geofence, mở chỉ đường và check-in khi ở gần điểm tham quan.",
                "homeApprovedPoi": "POI đã duyệt",
                "homeOpenTours": "Tour đang mở",
                "homeDiscoveredPoi": "POI bạn đã khám phá",
                "homePoints": "Điểm",
                "homePointsLower": "điểm",
                "homeFeatureKicker": "Tính năng nổi bật",
                "homeFeatureTitle": "Một tài khoản, nhiều trải nghiệm.",
                "homeFeatureDesc": "Giao diện du khách được tách riêng khỏi admin, nhưng vẫn dùng chung dữ liệu tài khoản với app mobile.",
                "homeFeatureMapTitle": "Bản đồ tham quan",
                "homeFeatureMapDesc": "Xem các điểm tham quan đã duyệt, vị trí, bán kính kích hoạt và mở chỉ đường nhanh.",
                "homeFeatureCheckinTitle": "Check-in vị trí",
                "homeFeatureCheckinDesc": "Du khách có thể dùng GPS để khám phá POI nằm trong vùng geofence.",
                "homeFeatureMobileTitle": "Dùng chung với app",
                "homeFeatureMobileDesc": "Tài khoản đăng ký trên web được lưu vào bảng du khách, app có thể đăng nhập cùng tài khoản.",
                "homePlaceKicker": "Địa điểm",
                "homePlaceTitle": "Điểm tham quan mới.",
                "homeViewOnMap": "Xem trên bản đồ",
                "homeDirection": "Chỉ đường",
                "homeNoDataKicker": "Chưa có dữ liệu hiển thị",
                "homeNoPoiTitle": "Hãy duyệt POI để trang du khách sống động hơn.",
                "homeNoPoiDescPrefix": "Hiện chưa có POI trạng thái",
                "homeNoPoiDescSuffix": "nên trang chủ chưa có địa điểm thật để hiển thị. Giao diện vẫn có đầy đủ khối giới thiệu, tính năng, hướng dẫn và nút mở bản đồ.",
                "homeManagePoi": "Vào quản lý POI",
                "homeViewMap": "Xem bản đồ",
                "homeHistoryKicker": "Lịch sử",
                "homeHistoryTitle": "Khám phá gần đây.",
                "homeNoCheckinTitle": "Bạn chưa check-in POI nào.",
                "homeNoCheckinDesc": "Mở bản đồ và thử check-in khi bạn ở gần điểm tham quan để nhận điểm.",
                "homeProcessKicker": "Quy trình",
                "homeProcessTitle": "Du khách sử dụng như thế nào?",
                "homeStep1Title": "Tạo tài khoản",
                "homeStep1Desc": "Đăng ký tài khoản du khách trên web bằng email và mật khẩu.",
                "homeStep2Title": "Mở bản đồ",
                "homeStep2Desc": "Xem các POI đã duyệt, tìm địa điểm và chọn điểm muốn khám phá.",
                "homeStep3Title": "Đến địa điểm",
                "homeStep3Desc": "Dùng chỉ đường để di chuyển tới vị trí POI ngoài thực tế.",
                "homeStep4Title": "Dùng trên app",
                "homeStep4Desc": "Tài khoản web có thể đăng nhập trên app để tiếp tục trải nghiệm.",
                "homeCtaTitle": "Sẵn sàng khám phá chưa?",
                "homeCtaDesc": "Mở bản đồ du khách để xem các điểm tham quan, hoặc tạo tài khoản để dùng chung với app mobile."
        },
        "en": {
                "navHome": "Home",
                "navMap": "Map",
                "navScanQr": "Scan QR",
                "navTour": "Tours",
                "navRanking": "Ranking",
                "navPlans": "Plans",
                "navOrders": "Orders",
                "navAccount": "Account",
                "navLogout": "Logout",
                "navLogin": "Login",
                "navRegister": "Register",
                "footerText": "Tourist map, traveler accounts, and a shared experience with the mobile app.",
                "homeEyebrow": "Tourist web portal",
                "homeHeroTitlePrefix": "Explore the city with a",
                "homeHeroTitleHighlight": "smart map.",
                "homeHeroDesc": "Travelers can register, log in, view POIs on the map, check in by location, and use the same account on the mobile app.",
                "homeOpenMap": "Open map",
                "homeTourList": "Tour list",
                "homeRanking": "Leaderboard",
                "homeCreateAccount": "Create tourist account",
                "homeMyProfile": "My profile",
                "homePhoneTitle": "POI map near you",
                "homePhoneDesc": "View locations, geofence radius, open directions, and check in when you are near an attraction.",
                "homeApprovedPoi": "Approved POIs",
                "homeOpenTours": "Active tours",
                "homeDiscoveredPoi": "POIs you explored",
                "homePoints": "Points",
                "homePointsLower": "points",
                "homeFeatureKicker": "Key features",
                "homeFeatureTitle": "One account, many experiences.",
                "homeFeatureDesc": "The tourist interface is separated from admin, while still sharing account data with the mobile app.",
                "homeFeatureMapTitle": "Tourist map",
                "homeFeatureMapDesc": "View approved attractions, locations, activation radius, and quickly open directions.",
                "homeFeatureCheckinTitle": "Location check-in",
                "homeFeatureCheckinDesc": "Travelers can use GPS to explore POIs inside the geofence area.",
                "homeFeatureMobileTitle": "Shared with app",
                "homeFeatureMobileDesc": "Accounts registered on the web are saved as tourist accounts, and the app can log in with the same account.",
                "homePlaceKicker": "Places",
                "homePlaceTitle": "New attractions.",
                "homeViewOnMap": "View on map",
                "homeDirection": "Directions",
                "homeNoDataKicker": "No display data yet",
                "homeNoPoiTitle": "Approve POIs to make the tourist page more lively.",
                "homeNoPoiDescPrefix": "There are currently no POIs with",
                "homeNoPoiDescSuffix": "status, so the homepage has no real places to display yet. The interface still includes introduction, features, guide blocks, and map buttons.",
                "homeManagePoi": "Manage POIs",
                "homeViewMap": "View map",
                "homeHistoryKicker": "History",
                "homeHistoryTitle": "Recent discoveries.",
                "homeNoCheckinTitle": "You have not checked in to any POI yet.",
                "homeNoCheckinDesc": "Open the map and try checking in when you are near an attraction to earn points.",
                "homeProcessKicker": "Process",
                "homeProcessTitle": "How do travelers use it?",
                "homeStep1Title": "Create account",
                "homeStep1Desc": "Register a tourist account on the web using email and password.",
                "homeStep2Title": "Open map",
                "homeStep2Desc": "View approved POIs, find places, and select the attraction you want to explore.",
                "homeStep3Title": "Go to location",
                "homeStep3Desc": "Use directions to travel to the POI location in real life.",
                "homeStep4Title": "Use on app",
                "homeStep4Desc": "The web account can also log in on the app to continue the experience.",
                "homeCtaTitle": "Ready to explore?",
                "homeCtaDesc": "Open the tourist map to view attractions, or create an account to use with the mobile app."
        },
        "fr": {
                "navHome": "Accueil",
                "navMap": "Carte",
                "navScanQr": "Scanner QR",
                "navTour": "Tours",
                "navRanking": "Classement",
                "navPlans": "Forfaits",
                "navOrders": "Commandes",
                "navAccount": "Compte",
                "navLogout": "Déconnexion",
                "navLogin": "Connexion",
                "navRegister": "Inscription",
                "footerText": "Carte touristique, comptes voyageurs et expérience partagée avec l’application mobile.",
                "homeEyebrow": "Portail web touristique",
                "homeHeroTitlePrefix": "Explorez la ville avec une",
                "homeHeroTitleHighlight": "carte intelligente.",
                "homeHeroDesc": "Les voyageurs peuvent s’inscrire, se connecter, consulter les POI sur la carte, faire un check-in par position et utiliser le même compte sur l’application mobile.",
                "homeOpenMap": "Ouvrir la carte",
                "homeTourList": "Liste des tours",
                "homeRanking": "Classement",
                "homeCreateAccount": "Créer un compte touriste",
                "homeMyProfile": "Mon profil",
                "homePhoneTitle": "Carte des POI près de vous",
                "homePhoneDesc": "Consultez les lieux, le rayon geofence, ouvrez l’itinéraire et faites un check-in près d’un point d’intérêt.",
                "homeApprovedPoi": "POI approuvés",
                "homeOpenTours": "Tours actifs",
                "homeDiscoveredPoi": "POI explorés",
                "homePoints": "Points",
                "homePointsLower": "points",
                "homeFeatureKicker": "Fonctionnalités",
                "homeFeatureTitle": "Un compte, plusieurs expériences.",
                "homeFeatureDesc": "L’interface touriste est séparée de l’administration, tout en partageant les données de compte avec l’application mobile.",
                "homeFeatureMapTitle": "Carte touristique",
                "homeFeatureMapDesc": "Affichez les attractions approuvées, les positions, le rayon d’activation et ouvrez rapidement l’itinéraire.",
                "homeFeatureCheckinTitle": "Check-in par position",
                "homeFeatureCheckinDesc": "Les voyageurs peuvent utiliser le GPS pour explorer les POI dans la zone geofence.",
                "homeFeatureMobileTitle": "Partagé avec l’app",
                "homeFeatureMobileDesc": "Les comptes créés sur le web sont enregistrés comme comptes touristes et peuvent être utilisés dans l’application.",
                "homePlaceKicker": "Lieux",
                "homePlaceTitle": "Nouvelles attractions.",
                "homeViewOnMap": "Voir sur la carte",
                "homeDirection": "Itinéraire",
                "homeNoDataKicker": "Aucune donnée à afficher",
                "homeNoPoiTitle": "Approuvez des POI pour rendre la page touriste plus vivante.",
                "homeViewMap": "Voir la carte",
                "homeHistoryKicker": "Historique",
                "homeHistoryTitle": "Découvertes récentes.",
                "homeNoCheckinTitle": "Vous n’avez encore fait aucun check-in.",
                "homeProcessKicker": "Processus",
                "homeProcessTitle": "Comment les voyageurs l’utilisent-ils ?",
                "homeStep1Title": "Créer un compte",
                "homeStep2Title": "Ouvrir la carte",
                "homeStep3Title": "Aller sur place",
                "homeStep4Title": "Utiliser l’app",
                "homeCtaTitle": "Prêt à explorer ?"
        },
        "zh": {
                "navHome": "首页",
                "navMap": "地图",
                "navScanQr": "扫描 QR",
                "navTour": "路线",
                "navRanking": "排行",
                "navPlans": "服务套餐",
                "navOrders": "订单",
                "navAccount": "账户",
                "navLogout": "退出",
                "navLogin": "登录",
                "navRegister": "注册",
                "footerText": "旅游地图、游客账户，并与移动应用共享体验。",
                "homeEyebrow": "游客网站",
                "homeHeroTitlePrefix": "使用",
                "homeHeroTitleHighlight": "智能地图探索城市。",
                "homeHeroDesc": "游客可以注册、登录、查看 POI 地图、通过位置签到，并在移动应用中使用同一个账户。",
                "homeOpenMap": "打开地图",
                "homeTourList": "路线列表",
                "homeRanking": "排行榜",
                "homeCreateAccount": "创建游客账户",
                "homeMyProfile": "我的资料",
                "homePhoneTitle": "你附近的 POI 地图",
                "homePhoneDesc": "查看位置、围栏半径、打开路线，并在靠近景点时签到。",
                "homeApprovedPoi": "已审核 POI",
                "homeOpenTours": "开放路线",
                "homeDiscoveredPoi": "已探索 POI",
                "homePoints": "积分",
                "homePointsLower": "积分",
                "homeFeatureKicker": "主要功能",
                "homeFeatureTitle": "一个账户，多种体验。",
                "homeFeatureDesc": "游客界面与后台分离，但仍与移动应用共享账户数据。",
                "homeFeatureMapTitle": "旅游地图",
                "homeFeatureMapDesc": "查看已审核景点、位置、触发半径，并快速打开路线。",
                "homeFeatureCheckinTitle": "位置签到",
                "homeFeatureCheckinDesc": "游客可以使用 GPS 在地理围栏范围内探索 POI。",
                "homeFeatureMobileTitle": "与应用共享",
                "homeFeatureMobileDesc": "网页注册的账户会保存为游客账户，移动应用也可以使用同一账户登录。",
                "homePlaceKicker": "地点",
                "homePlaceTitle": "新景点。",
                "homeViewOnMap": "在地图上查看",
                "homeDirection": "路线",
                "homeNoDataKicker": "暂无显示数据",
                "homeNoPoiTitle": "请审核 POI，让游客页面更丰富。",
                "homeViewMap": "查看地图",
                "homeHistoryKicker": "历史",
                "homeHistoryTitle": "最近探索。",
                "homeNoCheckinTitle": "你还没有签到任何 POI。",
                "homeProcessKicker": "流程",
                "homeProcessTitle": "游客如何使用？",
                "homeStep1Title": "创建账户",
                "homeStep2Title": "打开地图",
                "homeStep3Title": "前往地点",
                "homeStep4Title": "使用应用",
                "homeCtaTitle": "准备好探索了吗？"
        },
        "ja": {
                "navHome": "ホーム",
                "navMap": "地図",
                "navScanQr": "QR読み取り",
                "navTour": "ツアー",
                "navRanking": "ランキング",
                "navPlans": "プラン",
                "navOrders": "注文",
                "navAccount": "アカウント",
                "navLogout": "ログアウト",
                "navLogin": "ログイン",
                "navRegister": "登録",
                "footerText": "観光マップ、旅行者アカウント、モバイルアプリと連携した体験。",
                "homeEyebrow": "旅行者向けWeb",
                "homeHeroTitlePrefix": "スマートマップで",
                "homeHeroTitleHighlight": "街を探索しよう。",
                "homeHeroDesc": "旅行者は登録、ログイン、POIマップの閲覧、位置情報チェックイン、そして同じアカウントでモバイルアプリを利用できます。",
                "homeOpenMap": "地図を開く",
                "homeTourList": "ツアー一覧",
                "homeRanking": "ランキング",
                "homeCreateAccount": "旅行者アカウント作成",
                "homeMyProfile": "マイプロフィール",
                "homePhoneTitle": "近くのPOIマップ",
                "homePhoneDesc": "位置、ジオフェンス半径、ルート案内を確認し、観光地の近くでチェックインできます。",
                "homeApprovedPoi": "承認済みPOI",
                "homeOpenTours": "公開中ツアー",
                "homeDiscoveredPoi": "探索したPOI",
                "homePoints": "ポイント",
                "homePointsLower": "ポイント",
                "homeFeatureKicker": "主な機能",
                "homeFeatureTitle": "1つのアカウントで多様な体験。",
                "homeFeatureDesc": "旅行者向け画面は管理画面と分離されていますが、モバイルアプリとアカウントデータを共有します。",
                "homeFeatureMapTitle": "観光マップ",
                "homeFeatureMapDesc": "承認済み観光地、位置、起動半径を確認し、すばやくルートを開けます。",
                "homeFeatureCheckinTitle": "位置情報チェックイン",
                "homeFeatureCheckinDesc": "旅行者はGPSを使ってジオフェンス内のPOIを探索できます。",
                "homeFeatureMobileTitle": "アプリと共有",
                "homeFeatureMobileDesc": "Webで登録したアカウントは旅行者アカウントとして保存され、アプリでも同じアカウントでログインできます。",
                "homePlaceKicker": "スポット",
                "homePlaceTitle": "新しい観光地。",
                "homeViewOnMap": "地図で見る",
                "homeDirection": "ルート",
                "homeNoDataKicker": "表示データがありません",
                "homeNoPoiTitle": "POIを承認して旅行者ページを充実させましょう。",
                "homeViewMap": "地図を見る",
                "homeHistoryKicker": "履歴",
                "homeHistoryTitle": "最近の探索。",
                "homeNoCheckinTitle": "まだPOIにチェックインしていません。",
                "homeProcessKicker": "使い方",
                "homeProcessTitle": "旅行者はどう使いますか？",
                "homeStep1Title": "アカウント作成",
                "homeStep2Title": "地図を開く",
                "homeStep3Title": "現地へ移動",
                "homeStep4Title": "アプリで利用",
                "homeCtaTitle": "探索の準備はできましたか？"
        },
        "ko": {
                "navHome": "홈",
                "navMap": "지도",
                "navScanQr": "QR 스캔",
                "navTour": "투어",
                "navRanking": "랭킹",
                "navPlans": "서비스 플랜",
                "navOrders": "주문",
                "navAccount": "계정",
                "navLogout": "로그아웃",
                "navLogin": "로그인",
                "navRegister": "회원가입",
                "footerText": "관광 지도, 여행자 계정, 모바일 앱과 함께하는 통합 경험.",
                "homeEyebrow": "여행자 웹",
                "homeHeroTitlePrefix": "스마트 지도로",
                "homeHeroTitleHighlight": "도시를 탐험하세요.",
                "homeHeroDesc": "여행자는 가입, 로그인, POI 지도 보기, 위치 기반 체크인을 할 수 있으며 모바일 앱에서도 같은 계정을 사용할 수 있습니다.",
                "homeOpenMap": "지도 열기",
                "homeTourList": "투어 목록",
                "homeRanking": "랭킹",
                "homeCreateAccount": "여행자 계정 만들기",
                "homeMyProfile": "내 프로필",
                "homePhoneTitle": "내 주변 POI 지도",
                "homePhoneDesc": "위치, 지오펜스 반경, 길찾기를 확인하고 관광지 근처에서 체크인할 수 있습니다.",
                "homeApprovedPoi": "승인된 POI",
                "homeOpenTours": "운영 중인 투어",
                "homeDiscoveredPoi": "탐험한 POI",
                "homePoints": "포인트",
                "homePointsLower": "포인트",
                "homeFeatureKicker": "주요 기능",
                "homeFeatureTitle": "하나의 계정, 다양한 경험.",
                "homeFeatureDesc": "여행자 화면은 관리자와 분리되어 있지만 모바일 앱과 계정 데이터를 공유합니다.",
                "homeFeatureMapTitle": "관광 지도",
                "homeFeatureMapDesc": "승인된 관광지, 위치, 활성 반경을 보고 빠르게 길찾기를 열 수 있습니다.",
                "homeFeatureCheckinTitle": "위치 체크인",
                "homeFeatureCheckinDesc": "여행자는 GPS를 사용해 지오펜스 영역 안의 POI를 탐험할 수 있습니다.",
                "homeFeatureMobileTitle": "앱과 공유",
                "homeFeatureMobileDesc": "웹에서 가입한 계정은 여행자 계정으로 저장되며 앱에서도 같은 계정으로 로그인할 수 있습니다.",
                "homePlaceKicker": "장소",
                "homePlaceTitle": "새로운 관광지.",
                "homeViewOnMap": "지도에서 보기",
                "homeDirection": "길찾기",
                "homeNoDataKicker": "표시할 데이터 없음",
                "homeNoPoiTitle": "POI를 승인하여 여행자 페이지를 더 풍성하게 만드세요.",
                "homeViewMap": "지도 보기",
                "homeHistoryKicker": "기록",
                "homeHistoryTitle": "최근 탐험.",
                "homeNoCheckinTitle": "아직 체크인한 POI가 없습니다.",
                "homeProcessKicker": "사용 절차",
                "homeProcessTitle": "여행자는 어떻게 사용하나요?",
                "homeStep1Title": "계정 만들기",
                "homeStep2Title": "지도 열기",
                "homeStep3Title": "장소로 이동",
                "homeStep4Title": "앱에서 사용",
                "homeCtaTitle": "탐험할 준비가 되었나요?"
        }
};
    const phraseDictionaries = {
        "en": {
                "Smart Tour Guide": "Smart Tour Guide",
                "Du khách": "Traveler",
                "Trang chủ du khách": "Tourist home",
                "Trang chủ": "Home",
                "Bản đồ": "Map",
                "Quét QR": "Scan QR",
                "Tour": "Tours",
                "Xếp hạng": "Ranking",
                "Gói dịch vụ": "Plans",
                "Đơn hàng": "Orders",
                "Tài khoản": "Account",
                "Đăng xuất": "Logout",
                "Đăng nhập": "Login",
                "Đăng ký": "Register",
                "Đổi giao diện": "Change theme",
                "Đóng": "Close",
                "Quay lại": "Back",
                "Tiếng Việt": "Vietnamese",
                "English": "English",
                "Français": "French",
                "中文": "Chinese",
                "日本語": "Japanese",
                "한국어": "Korean",
                "Đăng nhập du khách": "Tourist login",
                "Đăng ký du khách": "Tourist registration",
                "Hồ sơ du khách": "Tourist profile",
                "Quên mật khẩu": "Forgot password",
                "Đặt lại mật khẩu": "Reset password",
                "Đổi mật khẩu du khách": "Change tourist password",
                "Bảo mật tài khoản": "Account security",
                "Khôi phục tài khoản": "Account recovery",
                "Tài khoản du khách": "Tourist account",
                "Đăng nhập để tiếp tục": "Log in to continue",
                "hành trình.": "your journey.",
                "Tạo tài khoản để": "Create an account to",
                "khám phá thông minh.": "explore smarter.",
                "Cập nhật mật khẩu": "Update your password",
                "an toàn hơn.": "more securely.",
                "Tạo mật khẩu mới": "Create a new password",
                "Lấy lại quyền truy cập": "Recover access to",
                "tài khoản du khách.": "your tourist account.",
                "Sử dụng tài khoản du khách để xem bản đồ POI, check-in bằng GPS,": "Use your tourist account to view the POI map, check in by GPS,",
                "nhận điểm khám phá và đăng nhập cùng tài khoản trên app mobile.": "earn exploration points, and use the same account on the mobile app.",
                "Đăng ký tài khoản du khách để xem bản đồ, check-in POI bằng GPS,": "Register a tourist account to view the map, check in to POIs by GPS,",
                "nhận điểm khám phá và dùng chung tài khoản trên app mobile.": "earn exploration points, and share the account with the mobile app.",
                "Nhập email và mật khẩu tài khoản du khách của bạn.": "Enter your tourist account email and password.",
                "Điền thông tin bên dưới để tạo tài khoản du khách mới.": "Fill in the information below to create a new tourist account.",
                "Nhập email đã đăng ký để nhận hướng dẫn đặt lại mật khẩu.": "Enter your registered email to receive password reset instructions.",
                "Nhập email bạn đã dùng để đăng ký tài khoản du khách.": "Enter the email you used to register your tourist account.",
                "Nhập mật khẩu mới cho tài khoản bên dưới.": "Enter a new password for the account below.",
                "Nhập mật khẩu hiện tại và mật khẩu mới để cập nhật tài khoản.": "Enter your current password and new password to update the account.",
                "Bản đồ POI": "POI map",
                "Xem điểm tham quan đã duyệt và mở chỉ đường nhanh.": "View approved attractions and open directions quickly.",
                "Check-in GPS": "GPS check-in",
                "Ghi nhận khám phá khi bạn ở gần địa điểm thật.": "Record your discovery when you are near the real location.",
                "Dùng chung app": "Shared with app",
                "Tài khoản web đăng nhập được trên app mobile.": "The web account can log in on the mobile app.",
                "Email và mật khẩu ở web du khách dùng chung với app.": "Email and password on the tourist web are shared with the app.",
                "Đăng ký trên web xong có thể đăng nhập trên app bằng cùng tài khoản.": "After registering on the web, you can log in on the app with the same account.",
                "Xem bản đồ": "View map",
                "Xem bản đồ POI": "View POI map",
                "Khám phá các điểm tham quan đã được duyệt trên hệ thống.": "Explore attractions approved in the system.",
                "Ghi nhận khi bạn ở gần vị trí thực tế của POI.": "Record your visit when you are near the real POI location.",
                "Tài khoản web có thể đăng nhập trên app mobile.": "The web account can log in on the mobile app.",
                "Tài khoản này lưu vào bảng": "This account is saved in the",
                "nên có thể đăng nhập": "so you can log in",
                "cả web du khách và app mobile bằng cùng email/mật khẩu.": "to both the tourist web and mobile app using the same email/password.",
                "Họ và tên": "Full name",
                "Địa chỉ email": "Email address",
                "Mật khẩu": "Password",
                "Mật khẩu hiện tại": "Current password",
                "Mật khẩu mới": "New password",
                "Nhập lại mật khẩu": "Confirm password",
                "Nhập lại mật khẩu mới": "Confirm new password",
                "Xác nhận mật khẩu mới": "Confirm new password",
                "Tạo tài khoản": "Create account",
                "Tạo tài khoản mới": "Create new account",
                "Đã có tài khoản?": "Already have an account?",
                "Chưa có tài khoản?": "No account yet?",
                "Đăng ký ngay": "Register now",
                "Nhớ mật khẩu rồi?": "Remembered your password?",
                "Quay lại đăng nhập": "Back to login",
                "Gửi yêu cầu khôi phục": "Send recovery request",
                "Lưu mật khẩu mới": "Save new password",
                "Lưu hồ sơ": "Save profile",
                "Đổi mật khẩu": "Change password",
                "Hồ sơ của tôi": "My profile",
                "Cập nhật thông tin": "Update information",
                "Thay đổi họ tên hoặc email tài khoản du khách.": "Change the full name or email of the tourist account.",
                "Email này cũng là email đăng nhập trên app mobile.": "This email is also used to log in on the mobile app.",
                "Email này dùng chung cho web du khách và app mobile.": "This email is shared between the tourist web and mobile app.",
                "Bạn có thể cập nhật thông tin, xem tiến độ khám phá và đổi mật khẩu tài khoản.": "You can update information, view exploration progress, and change your password.",
                "Xin chào,": "Hello,",
                "Tổng điểm": "Total points",
                "Hạng hiện tại": "Current rank",
                "POI đã khám phá": "POIs explored",
                "Tiến độ": "Progress",
                "Ngày tạo tài khoản:": "Account created:",
                "Tiến độ khám phá": "Exploration progress",
                "Truy cập nhanh": "Quick access",
                "Đi tới các chức năng quan trọng của tài khoản du khách.": "Go to important tourist account features.",
                "Nhập họ tên": "Enter full name",
                "Nhập mật khẩu": "Enter password",
                "Ít nhất 8 ký tự": "At least 8 characters",
                "Nhập lại mật khẩu để xác nhận.": "Enter the password again to confirm.",
                "Nhập lại mật khẩu mới để xác nhận.": "Enter the new password again to confirm.",
                "Nên dùng ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số hoặc ký tự đặc biệt.": "Use at least 8 characters, including uppercase, lowercase, numbers, or special characters.",
                "Mật khẩu còn yếu. Hãy thêm chữ hoa, số hoặc ký tự đặc biệt.": "Password is weak. Add uppercase letters, numbers, or special characters.",
                "Mật khẩu khá ổn. Có thể thêm ký tự đặc biệt để mạnh hơn.": "Password is fairly good. Add special characters to make it stronger.",
                "Mật khẩu mạnh.": "Strong password.",
                "Mật khẩu xác nhận khớp.": "Passwords match.",
                "Mật khẩu xác nhận chưa khớp.": "Passwords do not match.",
                "Hiện/ẩn mật khẩu": "Show/hide password",
                "Không chia sẻ mật khẩu với người khác. Nên dùng mật khẩu khác với email,": "Do not share your password. Use a different password from your email,",
                "mạng xã hội hoặc các tài khoản quan trọng khác.": "social networks, or other important accounts.",
                "Sau khi đổi mật khẩu thành công, hãy dùng mật khẩu mới để đăng nhập lại trên app mobile.": "After changing your password successfully, use the new password to log in again on the mobile app.",
                "Sau khi đặt lại thành công, hãy dùng mật khẩu mới để đăng nhập trên web và app mobile.": "After resetting successfully, use the new password to log in on the web and mobile app.",
                "Vì lý do bảo mật, hệ thống có thể không thông báo email có tồn tại hay không.": "For security reasons, the system may not say whether the email exists.",
                "Hãy nhập đúng email đã đăng ký tài khoản du khách.": "Please enter the email registered for the tourist account.",
                "Nếu không thấy email trong hộp thư đến, hãy kiểm tra mục Spam/Quảng cáo.": "If you do not see the email in your inbox, check Spam/Promotions.",
                "Link đặt lại mật khẩu thường chỉ có hiệu lực trong một khoảng thời gian nhất định.": "Password reset links are usually valid for a limited time.",
                "Bảo mật": "Security",
                "Mật khẩu mới đăng nhập được cả web và app mobile.": "The new password can log in to both the web and mobile app.",
                "Không chia sẻ mật khẩu mới cho người khác.": "Do not share the new password with anyone.",
                "Mật khẩu tốt nên có chữ hoa, chữ thường, số và ký tự đặc biệt.": "A good password should include uppercase, lowercase, numbers, and special characters.",
                "Web cho du khách": "Tourist web portal",
                "Khám phá thành phố bằng": "Explore the city with a",
                "bản đồ thông minh.": "smart map.",
                "Du khách có thể đăng ký, đăng nhập, xem bản đồ POI, check-in bằng vị trí": "Travelers can register, log in, view POIs on the map, check in by location",
                "và dùng cùng một tài khoản trên app mobile.": "and use the same account on the mobile app.",
                "Mở bản đồ": "Open map",
                "Danh sách Tour": "Tour list",
                "Bảng xếp hạng": "Leaderboard",
                "Tạo tài khoản du khách": "Create tourist account",
                "Bản đồ POI gần bạn": "POI map near you",
                "Xem vị trí, bán kính geofence, mở chỉ đường và check-in khi ở gần điểm tham quan.": "View locations, geofence radius, open directions, and check in when you are near an attraction.",
                "POI đã duyệt": "Approved POIs",
                "Tour đang mở": "Active tours",
                "POI bạn đã khám phá": "POIs you explored",
                "Điểm": "Points",
                "Tính năng nổi bật": "Key features",
                "Một tài khoản, nhiều trải nghiệm.": "One account, many experiences.",
                "Giao diện du khách được tách riêng khỏi admin, nhưng vẫn dùng chung dữ liệu tài khoản": "The tourist interface is separated from admin, while still sharing account data",
                "với app mobile.": "with the mobile app.",
                "Bản đồ tham quan": "Tourist map",
                "Xem các điểm tham quan đã duyệt, vị trí, bán kính kích hoạt và mở chỉ đường nhanh.": "View approved attractions, locations, activation radius, and quickly open directions.",
                "Check-in vị trí": "Location check-in",
                "Du khách có thể dùng GPS để khám phá POI nằm trong vùng geofence.": "Travelers can use GPS to explore POIs inside the geofence area.",
                "Dùng chung với app": "Shared with app",
                "Tài khoản đăng ký trên web được lưu vào bảng du khách, app có thể đăng nhập cùng tài khoản.": "Accounts registered on the web are saved as tourist accounts, and the app can log in with the same account.",
                "Địa điểm": "Places",
                "Điểm tham quan mới.": "New attractions.",
                "Xem trên bản đồ": "View on map",
                "Chưa có dữ liệu hiển thị": "No display data yet",
                "Hãy duyệt POI để trang du khách sống động hơn.": "Approve POIs to make the tourist page more lively.",
                "Hiện chưa có POI trạng thái": "There are currently no POIs with",
                "nên trang chủ chưa có địa điểm thật để hiển thị.": "status, so the homepage has no real places to display yet.",
                "Giao diện vẫn có đầy đủ khối giới thiệu, tính năng, hướng dẫn và nút mở bản đồ.": "The interface still includes introduction, features, guide blocks, and map buttons.",
                "Vào quản lý POI": "Manage POIs",
                "Lịch sử": "History",
                "Khám phá gần đây.": "Recent discoveries.",
                "Bạn chưa check-in POI nào.": "You have not checked in to any POI yet.",
                "Mở bản đồ và thử check-in khi bạn ở gần điểm tham quan để nhận điểm.": "Open the map and try checking in when you are near an attraction to earn points.",
                "Quy trình": "Process",
                "Du khách sử dụng như thế nào?": "How do travelers use it?",
                "Đăng ký tài khoản du khách trên web bằng email và mật khẩu.": "Register a tourist account on the web using email and password.",
                "Đến địa điểm": "Go to location",
                "Dùng chỉ đường để di chuyển tới vị trí POI ngoài thực tế.": "Use directions to travel to the POI location in real life.",
                "Dùng trên app": "Use on app",
                "Tài khoản web có thể đăng nhập trên app để tiếp tục trải nghiệm.": "The web account can also log in on the app to continue the experience.",
                "Sẵn sàng khám phá chưa?": "Ready to explore?",
                "Mở bản đồ du khách để xem các điểm tham quan, hoặc tạo tài khoản để dùng chung với app mobile.": "Open the tourist map to view attractions, or create an account to use with the mobile app.",
                "Tìm điểm tham quan": "Find attractions",
                "gần bạn.": "near you.",
                "Hiển thị các POI đã được duyệt. Đăng nhập du khách để check-in bằng GPS,": "Show approved POIs. Log in as a tourist to check in by GPS,",
                "xem chi tiết địa điểm, chọn ngôn ngữ thuyết minh và đồng bộ tiến độ với app mobile.": "view place details, choose narration language, and sync progress with the mobile app.",
                "POI trên bản đồ": "POIs on map",
                "Đã check-in": "Checked in",
                "Ngôn ngữ thuyết minh": "Narration language",
                "Danh sách POI": "POI list",
                "Khám phá trên bản đồ": "Explore on the map",
                "Chọn một địa điểm để xem mô tả, chỉ đường hoặc check-in bằng GPS.": "Choose a place to view descriptions, directions, or check in by GPS.",
                "Tìm POI theo tên hoặc mô tả...": "Search POIs by name or description...",
                "Vị trí của tôi": "My location",
                "Xem tất cả": "View all",
                "Đã lưu": "Saved",
                "Lưu lại": "Save",
                "4+ Sao": "4+ Stars",
                "Chọn một POI": "Choose a POI",
                "Bấm marker hoặc chọn trong danh sách để xem mô tả, nghe audio và check-in khi bạn ở gần địa điểm.": "Click a marker or choose from the list to view descriptions, listen to audio, and check in near the place.",
                "Menu / mua đồ": "Menu / Buy",
                "Xem video": "Watch video",
                "Nghe thuyết minh": "Listen to narration",
                "Để lại đánh giá của bạn": "Leave your review",
                "Gửi đánh giá": "Submit review",
                "Đánh giá của cộng đồng": "Community reviews",
                "Chưa có": "None",
                "đánh giá": "reviews",
                "Chưa check-in": "Not checked in",
                "Không tìm thấy POI phù hợp.": "No matching POIs found.",
                "Thử nhập tên địa điểm hoặc mô tả khác.": "Try another place name or description.",
                "Điểm tham quan": "Attraction",
                "Xem chi tiết": "View details",
                "Chia sẻ cảm nhận của bạn...": "Share your thoughts...",
                "Đang lấy vị trí của bạn...": "Getting your location...",
                "Trình duyệt không hỗ trợ lấy vị trí.": "Your browser does not support location access.",
                "Không lấy được vị trí. Hãy cấp quyền Location cho trình duyệt.": "Cannot get your location. Please allow location permission in the browser.",
                "Đang kiểm tra vị trí để check-in...": "Checking your location for check-in...",
                "Check-in thất bại.": "Check-in failed.",
                "Đang tính toán tuyến đường...": "Calculating route...",
                "Vui lòng lấy \"Vị trí của tôi\" trước khi chỉ đường.": "Please get “My location” before using directions.",
                "Không thể tìm thấy tuyến đường đến POI này.": "Could not find a route to this POI.",
                "Vị trí của bạn": "Your location",
                "Không có nội dung thuyết minh cho ngôn ngữ này.": "No narration content for this language.",
                "Chưa có nội dung thuyết minh cho ngôn ngữ này.": "No narration content for this language yet.",
                "Chưa có mô tả chi tiết cho ngôn ngữ này.": "No detailed description for this language yet.",
                "Đăng nhập để đánh giá và bình luận POI này.": "Log in to rate and comment on this POI.",
                "Vui lòng chọn số sao để đánh giá.": "Please choose a star rating.",
                "Lỗi khi gửi đánh giá.": "Error submitting review.",
                "Có lỗi xảy ra khi lưu địa điểm.": "An error occurred while saving the place.",
                "Chạm để xem chi tiết và chỉ đường.": "Tap to view details and directions.",
                "Đang đọc thuyết minh bằng": "Reading narration in",
                "Đã chuyển ngôn ngữ thuyết minh sang": "Narration language changed to",
                "Đã lấy vị trí. Độ chính xác khoảng": "Location acquired. Accuracy about",
                "Tuyến đường dài": "Route distance",
                "thời gian đi dự kiến": "estimated travel time",
                "giờ": "hours",
                "phút": "minutes",
                "Quét QR điểm tham quan.": "Scan attraction QR code.",
                "Dùng camera để quét mã QR tại điểm tham quan. Sau khi quét thành công,": "Use the camera to scan the QR code at an attraction. After a successful scan,",
                "hệ thống sẽ tự mở POI trên bản đồ, giữ đúng ngôn ngữ thuyết minh bạn đang chọn.": "the system will open the POI on the map while keeping your selected narration language.",
                "Camera quét QR": "QR camera",
                "Cho phép trình duyệt truy cập camera để bắt đầu quét.": "Allow the browser to access the camera to start scanning.",
                "Mở camera": "Open camera",
                "Dừng quét": "Stop scan",
                "Về bản đồ": "Back to map",
                "Nhập mã thủ công": "Enter code manually",
                "Dùng khi camera không mở được.": "Use this when the camera cannot be opened.",
                "Dán nội dung QR hoặc token POI...": "Paste QR content or POI token...",
                "Mở POI": "Open POI",
                "Lưu ý:": "Note:",
                "Camera dùng được trên": "Camera works on",
                "Khi đưa lên hosting thật,": "When deployed to real hosting,",
                "web cần chạy bằng": "the web must run with",
                "thì trình duyệt mới cho phép mở camera.": "so the browser can allow camera access.",
                "Đã quét được mã QR. Đang mở POI...": "QR scanned. Opening POI...",
                "Không tải được thư viện quét QR. Kiểm tra kết nối mạng hoặc CDN.": "Could not load the QR scanning library. Check network or CDN.",
                "Camera đã bật. Đưa mã QR vào khung quét.": "Camera is on. Place the QR code in the scan frame.",
                "Không mở được camera. Hãy cấp quyền Camera hoặc thử nhập mã thủ công.": "Cannot open camera. Allow camera permission or enter the code manually.",
                "Đã dừng quét QR.": "QR scanning stopped.",
                "Tour gợi ý": "Suggested tours",
                "Chọn lộ trình": "Choose a route",
                "khám phá phù hợp.": "that fits your trip.",
                "Các tour giúp du khách đi theo thứ tự POI hợp lý, xem điểm dừng trên bản đồ và bắt đầu điều hướng nhanh.": "Tours help travelers follow POIs in a reasonable order, view stops on the map, and start navigation quickly.",
                "Chưa có tour đang mở": "No active tours yet",
                "Hãy tạo tour trong khu vực quản trị và đặt trạng thái active để du khách có thể xem lộ trình tại đây.": "Create tours in the admin area and set them active so travelers can view routes here.",
                "điểm": "points",
                "Lộ trình gợi ý dành cho du khách.": "Suggested route for travelers.",
                "Xem lộ trình": "View route",
                "Trở về danh sách Tour": "Back to tour list",
                "Lộ trình hướng dẫn chi tiết dành cho du khách.": "Detailed guided route for travelers.",
                "Đi bộ": "Walking",
                "Xe đạp": "Bicycle",
                "Lái xe": "Driving",
                "Bắt đầu đi theo Tour": "Start following tour",
                "Các điểm đến": "Destinations",
                "Khám phá địa điểm này": "Explore this place",
                "Mở trên bản đồ": "Open on map",
                "Đang lấy vị trí...": "Getting location...",
                "Không lấy được vị trí GPS. Hãy kiểm tra quyền Location.": "Could not get GPS location. Please check Location permission.",
                "Hạng": "Rank",
                "Điểm khám phá": "Exploration points",
                "Số POI": "POI count",
                "Chưa có dữ liệu xếp hạng": "No ranking data yet",
                "Hãy check-in POI để xuất hiện trên bảng xếp hạng.": "Check in to POIs to appear on the leaderboard.",
                "Chi tiết xếp hạng": "Ranking details",
                "Lần check-in": "Check-ins",
                "Ngày gần nhất": "Latest date",
                "Không có dữ liệu": "No data",
                "Mở chat AI": "Open AI chat",
                "Chatbox AI du khách": "Tourist AI chatbox",
                "Trợ lý AI du lịch": "Travel AI Assistant",
                "Hỏi về POI, tour, bản đồ và thuyết minh": "Ask about POIs, tours, maps, and narration",
                "Đóng chat AI": "Close AI chat",
                "Xin chào! Mình là trợ lý AI của VERSA Travel. Bạn có thể hỏi: “Nên đi đâu ở Sài Gòn?”, “Tour nào phù hợp?”, hoặc “Cách nghe thuyết minh?”": "Hello! I am the VERSA Travel AI assistant. You can ask: “Where should I go in Saigon?”, “Which tour is suitable?”, or “How do I listen to narration?”",
                "Gợi ý POI": "Suggest POIs",
                "Chọn tour": "Choose tour",
                "Nhập câu hỏi của bạn...": "Type your question...",
                "Gửi câu hỏi": "Send question",
                "Đơn menu": "Menu orders",
                "Đơn mua menu": "Menu orders",
                "Thanh toán": "Payments",
                "Gói & thanh toán": "Plans & payments",
                "Tổng tiền": "Total",
                "Trạng thái": "Status",
                "Ngày tạo": "Created date",
                "Chi tiết": "Details",
                "Hủy đơn": "Cancel order",
                "Đặt món": "Order",
                "Mua ngay": "Buy now",
                "Số lượng": "Quantity",
                "Ghi chú": "Note",
                "Số điện thoại": "Phone number",
                "Gửi đơn": "Submit order",
                "Món đã chọn": "Selected items",
                "Giỏ hàng": "Cart",
                "Tạm tính": "Subtotal",
                "Địa chỉ nhận": "Receiving address",
                "Mã đơn": "Order code",
                "Đang chờ": "Pending",
                "Đã thanh toán": "Paid",
                "Đã hủy": "Cancelled",
                "Hoàn tất": "Completed"
        },
        "fr": {
                "Trang chủ": "Accueil",
                "Bản đồ": "Carte",
                "Quét QR": "Scanner QR",
                "Tour": "Tours",
                "Xếp hạng": "Classement",
                "Gói dịch vụ": "Forfaits",
                "Đơn hàng": "Commandes",
                "Tài khoản": "Compte",
                "Đăng xuất": "Déconnexion",
                "Đăng nhập": "Connexion",
                "Đăng ký": "Inscription",
                "Mở bản đồ": "Ouvrir la carte",
                "Danh sách Tour": "Liste des tours",
                "Bảng xếp hạng": "Classement",
                "Hồ sơ của tôi": "Mon profil",
                "Chỉ đường": "Itinéraire",
                "Đổi mật khẩu": "Changer le mot de passe",
                "Lưu hồ sơ": "Enregistrer le profil",
                "Họ và tên": "Nom complet",
                "Mật khẩu": "Mot de passe",
                "Mật khẩu mới": "Nouveau mot de passe",
                "Địa chỉ email": "Adresse e-mail",
                "Quay lại": "Retour",
                "Đóng": "Fermer",
                "Vị trí của tôi": "Ma position",
                "Xem tất cả": "Tout afficher",
                "Đã lưu": "Enregistré",
                "Lưu lại": "Enregistrer",
                "Gửi đánh giá": "Envoyer l’avis",
                "Quét QR điểm tham quan.": "Scanner le QR de l’attraction.",
                "Camera quét QR": "Caméra QR",
                "Mở camera": "Ouvrir la caméra",
                "Dừng quét": "Arrêter",
                "Về bản đồ": "Retour à la carte",
                "Nhập mã thủ công": "Saisir le code",
                "Mở POI": "Ouvrir le POI",
                "Trợ lý AI du lịch": "Assistant IA de voyage",
                "Nhập câu hỏi của bạn...": "Saisissez votre question...",
                "Gửi câu hỏi": "Envoyer la question"
        },
        "zh": {
                "Trang chủ": "首页",
                "Bản đồ": "地图",
                "Quét QR": "扫描 QR",
                "Tour": "路线",
                "Xếp hạng": "排行",
                "Gói dịch vụ": "服务套餐",
                "Đơn hàng": "订单",
                "Tài khoản": "账户",
                "Đăng xuất": "退出",
                "Đăng nhập": "登录",
                "Đăng ký": "注册",
                "Mở bản đồ": "打开地图",
                "Danh sách Tour": "路线列表",
                "Bảng xếp hạng": "排行榜",
                "Hồ sơ của tôi": "我的资料",
                "Chỉ đường": "路线",
                "Đổi mật khẩu": "修改密码",
                "Lưu hồ sơ": "保存资料",
                "Họ và tên": "姓名",
                "Mật khẩu": "密码",
                "Mật khẩu mới": "新密码",
                "Địa chỉ email": "电子邮箱",
                "Quay lại": "返回",
                "Đóng": "关闭",
                "Vị trí của tôi": "我的位置",
                "Xem tất cả": "查看全部",
                "Đã lưu": "已收藏",
                "Lưu lại": "收藏",
                "Gửi đánh giá": "提交评价",
                "Quét QR điểm tham quan.": "扫描景点 QR。",
                "Camera quét QR": "QR 摄像头",
                "Mở camera": "打开摄像头",
                "Dừng quét": "停止扫描",
                "Về bản đồ": "返回地图",
                "Nhập mã thủ công": "手动输入代码",
                "Mở POI": "打开 POI",
                "Trợ lý AI du lịch": "旅游 AI 助手",
                "Nhập câu hỏi của bạn...": "输入你的问题...",
                "Gửi câu hỏi": "发送问题"
        },
        "ja": {
                "Trang chủ": "ホーム",
                "Bản đồ": "地図",
                "Quét QR": "QR読み取り",
                "Tour": "ツアー",
                "Xếp hạng": "ランキング",
                "Gói dịch vụ": "プラン",
                "Đơn hàng": "注文",
                "Tài khoản": "アカウント",
                "Đăng xuất": "ログアウト",
                "Đăng nhập": "ログイン",
                "Đăng ký": "登録",
                "Mở bản đồ": "地図を開く",
                "Danh sách Tour": "ツアー一覧",
                "Bảng xếp hạng": "ランキング",
                "Hồ sơ của tôi": "マイプロフィール",
                "Chỉ đường": "ルート",
                "Đổi mật khẩu": "パスワード変更",
                "Lưu hồ sơ": "プロフィール保存",
                "Họ và tên": "氏名",
                "Mật khẩu": "パスワード",
                "Mật khẩu mới": "新しいパスワード",
                "Địa chỉ email": "メールアドレス",
                "Quay lại": "戻る",
                "Đóng": "閉じる",
                "Vị trí của tôi": "現在地",
                "Xem tất cả": "すべて表示",
                "Đã lưu": "保存済み",
                "Lưu lại": "保存",
                "Gửi đánh giá": "レビュー送信",
                "Quét QR điểm tham quan.": "観光地のQRを読み取ります。",
                "Camera quét QR": "QRカメラ",
                "Mở camera": "カメラを開く",
                "Dừng quét": "停止",
                "Về bản đồ": "地図へ戻る",
                "Nhập mã thủ công": "コードを手入力",
                "Mở POI": "POIを開く",
                "Trợ lý AI du lịch": "旅行AIアシスタント",
                "Nhập câu hỏi của bạn...": "質問を入力...",
                "Gửi câu hỏi": "質問を送信"
        },
        "ko": {
                "Trang chủ": "홈",
                "Bản đồ": "지도",
                "Quét QR": "QR 스캔",
                "Tour": "투어",
                "Xếp hạng": "랭킹",
                "Gói dịch vụ": "서비스 플랜",
                "Đơn hàng": "주문",
                "Tài khoản": "계정",
                "Đăng xuất": "로그아웃",
                "Đăng nhập": "로그인",
                "Đăng ký": "회원가입",
                "Mở bản đồ": "지도 열기",
                "Danh sách Tour": "투어 목록",
                "Bảng xếp hạng": "랭킹",
                "Hồ sơ của tôi": "내 프로필",
                "Chỉ đường": "길찾기",
                "Đổi mật khẩu": "비밀번호 변경",
                "Lưu hồ sơ": "프로필 저장",
                "Họ và tên": "이름",
                "Mật khẩu": "비밀번호",
                "Mật khẩu mới": "새 비밀번호",
                "Địa chỉ email": "이메일 주소",
                "Quay lại": "뒤로",
                "Đóng": "닫기",
                "Vị trí của tôi": "내 위치",
                "Xem tất cả": "전체 보기",
                "Đã lưu": "저장됨",
                "Lưu lại": "저장",
                "Gửi đánh giá": "리뷰 보내기",
                "Quét QR điểm tham quan.": "관광지 QR을 스캔합니다.",
                "Camera quét QR": "QR 카메라",
                "Mở camera": "카메라 열기",
                "Dừng quét": "스캔 중지",
                "Về bản đồ": "지도 돌아가기",
                "Nhập mã thủ công": "코드 직접 입력",
                "Mở POI": "POI 열기",
                "Trợ lý AI du lịch": "여행 AI 도우미",
                "Nhập câu hỏi của bạn...": "질문을 입력하세요...",
                "Gửi câu hỏi": "질문 보내기"
        }
};

    const keyDict = keyDictionaries[lang] || keyDictionaries.vi || {};
    const exactDict = phraseDictionaries[lang] || {};
    const englishDict = phraseDictionaries.en || {};

    const skipTags = new Set(['SCRIPT', 'STYLE', 'TEXTAREA', 'CODE', 'PRE', 'SVG', 'CANVAS']);
    let running = false;

    function normalizeText(value) {
        return String(value || '').replace(/\s+/g, ' ').trim();
    }

    function translateKey(key, fallback) {
        if (!key) return fallback || '';
        return keyDict[key] || keyDictionaries.en?.[key] || fallback || '';
    }

    function preserveEdgeSpace(original, translated) {
        const prefix = String(original).match(/^\s*/)?.[0] || '';
        const suffix = String(original).match(/\s*$/)?.[0] || '';
        return prefix + translated + suffix;
    }

    function translateExact(text) {
        const clean = normalizeText(text);
        if (!clean) return null;
        if (lang === 'vi') return null;
        return exactDict[clean] || englishDict[clean] || null;
    }

    function translateSegmentText(text) {
        if (lang === 'vi') return text;
        if (!text || !normalizeText(text)) return text;

        const exact = translateExact(text);
        if (exact) return preserveEdgeSpace(text, exact);

        let result = text;
        const entries = Object.entries(Object.assign({}, englishDict, exactDict))
            .filter(([source]) => source && source.length >= 4)
            .sort((a, b) => b[0].length - a[0].length);

        for (const [source, translated] of entries) {
            if (!translated) continue;
            if (result.includes(source)) {
                result = result.split(source).join(translated);
            }
        }

        return result;
    }

    function setTextWithoutBreakingChildren(el, value) {
        if (!el || !value) return;

        // Quan trọng: không dùng el.textContent cho element có icon/link/con bên trong,
        // vì nó sẽ xoá toàn bộ node con và có thể làm nút/href/dropdown hỏng.
        if (el.children.length === 0) {
            el.textContent = value;
            return;
        }

        const directTextNodes = Array.from(el.childNodes)
            .filter(node => node.nodeType === Node.TEXT_NODE && normalizeText(node.nodeValue));

        if (directTextNodes.length > 0) {
            directTextNodes[0].nodeValue = preserveEdgeSpace(directTextNodes[0].nodeValue, value);
            return;
        }

        const textTarget = el.querySelector(':scope > span:not(.fa):not(.fas):not(.far):not(.fab), :scope > strong, :scope > small');
        if (textTarget && textTarget.children.length === 0) {
            textTarget.textContent = value;
        }
    }

    function shouldSkipElement(el) {
        if (!el || el.nodeType !== Node.ELEMENT_NODE) return true;
        if (skipTags.has(el.tagName)) return true;
        if (el.closest('[data-no-i18n], .notranslate, [data-language-native], .dk-language, .map-language-dropdown')) return true;
        return false;
    }

    function translateAttributes(el) {
        if (shouldSkipElement(el)) return;

        const key = el.getAttribute('data-i18n');
        if (key) {
            const value = translateKey(key, normalizeText(el.textContent));
            if (value) setTextWithoutBreakingChildren(el, value);
        }

        const attrMap = [
            ['data-i18n-placeholder', 'placeholder'],
            ['data-i18n-title', 'title'],
            ['data-i18n-aria-label', 'aria-label'],
            ['data-i18n-value', 'value']
        ];

        for (const [keyAttr, targetAttr] of attrMap) {
            const attrKey = el.getAttribute(keyAttr);
            if (!attrKey) continue;
            const value = translateKey(attrKey, el.getAttribute(targetAttr) || '');
            if (value) el.setAttribute(targetAttr, value);
        }

        // Không dịch value của input hidden/anti-forgery/token để tránh lỗi form.
        const type = String(el.getAttribute('type') || '').toLowerCase();
        const canTranslateValue = ['button', 'submit', 'reset'].includes(type);

        ['placeholder', 'title', 'aria-label', 'data-ai-suggest'].forEach(attr => {
            if (!el.hasAttribute(attr)) return;
            const oldValue = el.getAttribute(attr);
            const newValue = translateSegmentText(oldValue);
            if (newValue && newValue !== oldValue) el.setAttribute(attr, newValue);
        });

        if (canTranslateValue && el.hasAttribute('value')) {
            const oldValue = el.getAttribute('value');
            const newValue = translateSegmentText(oldValue);
            if (newValue && newValue !== oldValue) el.setAttribute('value', newValue);
        }
    }

    function translateTextNode(node) {
        if (!node || node.nodeType !== Node.TEXT_NODE) return;

        const parent = node.parentElement;
        if (!parent || shouldSkipElement(parent)) return;

        const oldValue = node.nodeValue;
        const newValue = translateSegmentText(oldValue);

        if (newValue && newValue !== oldValue) {
            node.nodeValue = newValue;
        }
    }

    function translateElement(root) {
        if (lang === 'vi' || running || !root) return;
        running = true;

        try {
            if (root.nodeType === Node.TEXT_NODE) {
                translateTextNode(root);
                return;
            }

            if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE) {
                return;
            }

            if (root.nodeType === Node.ELEMENT_NODE) {
                translateAttributes(root);
            }

            const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
            let current;

            while ((current = walker.nextNode())) {
                if (current.nodeType === Node.ELEMENT_NODE) {
                    translateAttributes(current);
                } else if (current.nodeType === Node.TEXT_NODE) {
                    translateTextNode(current);
                }
            }

            if (document.title) {
                document.title = translateSegmentText(document.title);
            }
        } finally {
            running = false;
        }
    }

    function injectSafetyCss() {
        if (document.getElementById('versa-dukhach-i18n-safe-css')) return;

        const style = document.createElement('style');
        style.id = 'versa-dukhach-i18n-safe-css';
        style.textContent = `
            .tourist-hero h1,
            .login-hero h1,
            .register-hero h1,
            .forgot-hero h1,
            .reset-hero h1,
            .change-password-hero h1,
            .tourist-title,
            .cta-box h2,
            .empty-content h3,
            .qr-hero h1,
            .map-hero h1 {
                line-height: 1.08 !important;
                letter-spacing: -0.035em !important;
                overflow: visible !important;
                text-wrap: balance;
            }

            a,
            button,
            input,
            select,
            textarea,
            [role="button"],
            .dk-nav-link,
            .dk-btn,
            .map-btn,
            .pill {
                pointer-events: auto !important;
            }

            .ai-chat-window.hidden,
            .dk-backdrop:not(.show),
            .owner-backdrop:not(.show) {
                pointer-events: none !important;
            }
        `;
        document.head.appendChild(style);
    }

    function run() {
        injectSafetyCss();
        translateElement(document.body);
    }

    window.VersaDuKhachI18n = {
        lang,
        t: function (text) { return translateSegmentText(text); },
        tKey: function (key, fallback) { return translateKey(key, fallback); },
        refresh: run
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', run, { once: true });
    } else {
        run();
    }

    // Chỉ chạy thêm 1 lần sau khi các partial/plugin tải xong.
    // Không dùng MutationObserver liên tục để tránh làm trang bị đơ và không bấm được nút.
    window.addEventListener('load', function () {
        setTimeout(run, 150);
    }, { once: true });
})();
