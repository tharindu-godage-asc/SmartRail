/// Every word the app shows, in English, Sinhala and Tamil (FR-69, AC-E06-05 1). English is the source.
///
/// UNVERIFIED: apart from the phrases that also appear on the design canvas, the Sinhala and Tamil texts were written for this slice and
/// have NOT been checked by a native speaker (NFR-34, EC-117). They are placeholders until they are. A test fails if a key lacks a language.
enum Lang {
  en('en', 'English'),
  si('si', 'සිංහල'),
  ta('ta', 'தமிழ்');

  const Lang(this.code, this.label);

  final String code;
  final String label;

  static Lang? fromCode(String? code) {
    for (final l in values) {
      if (l.code == code) return l;
    }
    return null;
  }
}

enum K {
  appName,
  tagline,
  chooseLanguage,
  languageHint,
  cont,
  skip,
  next,
  getStarted,
  onboard1Title,
  onboard1Body,
  onboard2Title,
  onboard2Body,
  onboard3Title,
  onboard3Body,
  legendOnTime,
  legendDelayed,
  legendWaiting,
  tabTrains,
  tabTickets,
  tabHome,
  tabAlerts,
  tabAccount,
  findTrain,
  lineNormal,
  lineSomeDelayed,
  lineNone,
  lineUnavailable,
  yourTrain,
  from,
  to,
  chooseFrom,
  chooseTo,
  searchStations,
  recent,
  usualStart,
  showTrains,
  trains,
  noTrainsFound,
  noMoreToday,
  firstTomorrow,
  sched,
  platform,
  trainN,
  arrivesAt,
  arrivalAt,
  delayedFor,
  aboutLate,
  onTime,
  scheduled,
  waiting,
  arrived,
  passedStop,
  noNewsYet,
  lastSeenDeparted,
  lastSeenArrived,
  updatedAt,
  lastUpdate,
  offlineBanner,
  unavailableBanner,
  lastKnown,
  notLive,
  stops,
  departed,
  follow,
  following,
  stopFollowing,
  foodMenu,
  foodMenuAt,
  menuFrom,
  menuMayHaveChanged,
  available,
  soldOut,
  longStop,
  orderOnWebsite,
  orderNote,
  orderOffline,
  orderFailed,
  price,
  notFoundTitle,
  notFoundBody,
  arrivedBody,
  alertsTitle,
  trainsYouFollow,
  noFollowed,
  noAlerts,
  settingsTitle,
  language,
  followedTrains,
  remove,
  privacyTitle,
  privacyBody,
  ticketsTitle,
  ticketsBody,
  connectOnce,
  retry,
  back,
  trainsToday,
}

/// English, Sinhala, Tamil. `{x}` marks a value filled in by [Strings.fmt].
const Map<K, (String, String, String)> _table = {
  K.appName: ('SmartRail', 'SmartRail', 'SmartRail'),
  K.tagline: (
    'Live train times you can trust',
    'විශ්වාස කළ හැකි සජීවී දුම්රිය වේලාවන්',
    'நம்பகமான நேரடி ரயில் நேரங்கள்',
  ),
  K.chooseLanguage: (
    'Choose your language',
    'ඔබේ භාෂාව තෝරන්න',
    'உங்கள் மொழியைத் தேர்ந்தெடுக்கவும்',
  ),
  K.languageHint: (
    'You can change this later in Settings',
    'මෙය පසුව සැකසුම් තුළ වෙනස් කළ හැක',
    'இதை பின்னர் அமைப்புகளில் மாற்றலாம்',
  ),
  K.cont: ('Continue', 'ඉදිරියට', 'தொடரவும்'),
  K.skip: ('Skip', 'මඟ හරින්න', 'தவிர்க்கவும்'),
  K.next: ('Next', 'ඊළඟ', 'அடுத்து'),
  K.getStarted: ('Get started', 'ආරම්භ කරන්න', 'தொடங்கவும்'),
  K.onboard1Title: (
    'Know when your train is coming',
    'ඔබේ දුම්රිය පැමිණෙන වේලාව දැනගන්න',
    'உங்கள் ரயில் எப்போது வரும் என்பதை அறியுங்கள்',
  ),
  K.onboard1Body: (
    'See the expected arrival at your station, updated live.',
    'ඔබේ ස්ථානයට අපේක්ෂිත පැමිණීම සජීවීව බලන්න.',
    'உங்கள் நிலையத்தில் எதிர்பார்க்கப்படும் வருகையை நேரடியாகப் பாருங்கள்.',
  ),
  K.onboard2Title: (
    'Honest when we are unsure',
    'අපට විශ්වාස නැති විට අවංකව කියමු',
    'உறுதியில்லாதபோது நேர்மையாகச் சொல்கிறோம்',
  ),
  K.onboard2Body: (
    'Every status has an icon and words. If we do not know, we say so.',
    'සෑම තත්ත්වයකටම නිරූපකයක් සහ වචන ඇත. නොදන්නේ නම් එය කියමු.',
    'ஒவ்வொரு நிலைக்கும் ஐகானும் சொற்களும் உண்டு. தெரியாவிட்டால் அதைச் சொல்வோம்.',
  ),
  K.onboard3Title: (
    'Follow your train',
    'ඔබේ දුම්රිය අනුගමනය කරන්න',
    'உங்கள் ரயிலைப் பின்தொடருங்கள்',
  ),
  K.onboard3Body: (
    'Follow a train to keep it on your home screen.',
    'දුම්රියක් අනුගමනය කර එය ඔබේ මුල් තිරයේ තබා ගන්න.',
    'ஒரு ரயிலைப் பின்தொடர்ந்து முகப்புத் திரையில் வைத்திருங்கள்.',
  ),
  K.legendOnTime: ('On time', 'නියමිත වේලාවට', 'சரியான நேரத்தில்'),
  K.legendDelayed: ('Delayed', 'ප්‍රමාදයි', 'தாமதம்'),
  K.legendWaiting: (
    'Waiting for update',
    'යාවත්කාලීන කිරීමක් බලාපොරොත්තුවෙන්',
    'புதுப்பிப்புக்காகக் காத்திருக்கிறது',
  ),
  K.tabTrains: ('Trains', 'දුම්රිය', 'ரயில்கள்'),
  K.tabTickets: ('My tickets', 'මගේ ටිකට්', 'என் டிக்கெட்டுகள்'),
  K.tabHome: ('Home', 'මුල් පිටුව', 'முகப்பு'),
  K.tabAlerts: ('Alerts', 'දැනුම්දීම්', 'அறிவிப்புகள்'),
  K.tabAccount: ('Account', 'ගිණුම', 'கணக்கு'),
  K.findTrain: ('Find a train', 'දුම්රියක් සොයන්න', 'ரயிலைத் தேடுங்கள்'),
  K.lineNormal: (
    'Main Line: running normally',
    'ප්‍රධාන මාර්ගය: සාමාන්‍යයෙන් ධාවනය වේ',
    'பிரதான பாதை: வழக்கம்போல் இயங்குகிறது',
  ),
  K.lineSomeDelayed: (
    'Main Line: some trains delayed',
    'ප්‍රධාන මාර්ගය: සමහර දුම්රිය ප්‍රමාදයි',
    'பிரதான பாதை: சில ரயில்கள் தாமதம்',
  ),
  K.lineNone: (
    'No trains running now',
    'දැන් ධාවනය වන දුම්රිය නැත',
    'இப்போது இயங்கும் ரயில்கள் இல்லை',
  ),
  K.lineUnavailable: (
    'Line status unavailable',
    'මාර්ග තත්ත්වය නොමැත',
    'பாதை நிலை கிடைக்கவில்லை',
  ),
  K.yourTrain: ('Your train', 'ඔබේ දුම්රිය', 'உங்கள் ரயில்'),
  K.from: ('From', 'සිට', 'இருந்து'),
  K.to: ('To', 'දක්වා', 'வரை'),
  K.chooseFrom: (
    'Choose where you start',
    'ඔබ ආරම්භ කරන තැන තෝරන්න',
    'தொடங்கும் இடத்தைத் தேர்ந்தெடுக்கவும்',
  ),
  K.chooseTo: (
    'Choose where you are going',
    'ඔබ යන තැන තෝරන්න',
    'செல்லும் இடத்தைத் தேர்ந்தெடுக்கவும்',
  ),
  K.searchStations: (
    'Search stations',
    'ස්ථාන සොයන්න',
    'நிலையங்களைத் தேடுங்கள்',
  ),
  K.recent: ('Recent', 'මෑත', 'சமீபத்தியவை'),
  K.usualStart: (
    'Your usual starting station',
    'ඔබ සාමාන්‍යයෙන් ආරම්භ කරන ස්ථානය',
    'வழக்கமாகத் தொடங்கும் நிலையம்',
  ),
  K.showTrains: ('Show trains', 'දුම්රිය පෙන්වන්න', 'ரயில்களைக் காட்டு'),
  K.trains: ('Trains', 'දුම්රිය', 'ரயில்கள்'),
  K.noTrainsFound: (
    'No trains found',
    'දුම්රිය හමු නොවීය',
    'ரயில்கள் எதுவும் இல்லை',
  ),
  K.noMoreToday: (
    'No more trains today',
    'අද තවත් දුම්රිය නැත',
    'இன்று மேலும் ரயில்கள் இல்லை',
  ),
  K.firstTomorrow: (
    'First train tomorrow',
    'හෙට පළමු දුම්රිය',
    'நாளை முதல் ரயில்',
  ),
  K.sched: ('Sched.', 'නියමිත', 'திட்டம்'),
  K.platform: ('Platform {n}', 'වේදිකාව {n}', 'நடைமேடை {n}'),
  K.trainN: ('Train {n}', 'දුම්රිය {n}', 'ரயில் {n}'),
  K.arrivesAt: (
    'Arrives at {station}',
    '{station} වෙත පැමිණේ',
    '{station} வந்தடையும்',
  ),
  K.arrivalAt: ('Arrival at {station}', '{station} පැමිණීම', '{station} வருகை'),
  K.delayedFor: ('Delayed {t}', '{t} ප්‍රමාදයි', '{t} தாமதம்'),
  K.aboutLate: (
    'About {t} late. Updated {time}.',
    '{t} පමණ ප්‍රමාදයි. යාවත්කාලීන {time}.',
    'சுமார் {t} தாமதம். புதுப்பிப்பு {time}.',
  ),
  K.onTime: ('On time', 'නියමිත වේලාවට', 'சரியான நேரத்தில்'),
  K.scheduled: ('Scheduled', 'කාලසටහනට අනුව', 'அட்டவணைப்படி'),
  K.waiting: (
    'Waiting for update',
    'යාවත්කාලීන කිරීමක් බලාපොරොත්තුවෙන්',
    'புதுப்பிப்புக்காகக் காத்திருக்கிறது',
  ),
  K.arrived: ('Arrived', 'පැමිණියේය', 'வந்தடைந்தது'),
  K.passedStop: ('Passed', 'පසු විය', 'கடந்தது'),
  K.noNewsYet: (
    'No news yet. Last update {time}.',
    'තොරතුරු නැත. අවසන් යාවත්කාලීනය {time}.',
    'செய்தி இல்லை. கடைசிப் புதுப்பிப்பு {time}.',
  ),
  K.lastSeenDeparted: (
    'Last seen: departed {station} at {time}.',
    'අවසන් වරට: {station} සිට {time} ට පිටත් විය.',
    'கடைசியாக: {station} இலிருந்து {time} க்குப் புறப்பட்டது.',
  ),
  K.lastSeenArrived: (
    'Last seen: arrived at {station} at {time}.',
    'අවසන් වරට: {station} වෙත {time} ට පැමිණියේය.',
    'கடைசியாக: {station} க்கு {time} க்கு வந்தது.',
  ),
  K.updatedAt: ('Updated {time}', 'යාවත්කාලීන {time}', 'புதுப்பிப்பு {time}'),
  K.lastUpdate: (
    'Last update {time}',
    'අවසන් යාවත්කාලීනය {time}',
    'கடைசிப் புதுப்பிப்பு {time}',
  ),
  K.offlineBanner: (
    "You're offline · Showing info from {time}",
    'ඔබ ඔෆ්ලයින් · {time} සිට තොරතුරු පෙන්වයි',
    'நீங்கள் ஆஃப்லைனில் · {time} முதல் தகவல்',
  ),
  K.unavailableBanner: (
    'Live updates unavailable · Showing info from {time}',
    'සජීවී යාවත්කාලීන නොමැත · {time} සිට තොරතුරු පෙන්වයි',
    'நேரடி புதுப்பிப்புகள் இல்லை · {time} முதல் தகவல்',
  ),
  K.lastKnown: ('Last known', 'අවසන් දන්නා', 'கடைசியாக அறியப்பட்டது'),
  K.notLive: ('Not live', 'සජීවී නොවේ', 'நேரலை இல்லை'),
  K.stops: ('Stops', 'නැවතුම්', 'நிறுத்தங்கள்'),
  K.departed: ('Departed', 'පිටත් විය', 'புறப்பட்டது'),
  K.follow: (
    'Follow this train',
    'මෙම දුම්රිය අනුගමනය කරන්න',
    'இந்த ரயிலைப் பின்தொடருங்கள்',
  ),
  K.following: ('Following', 'අනුගමනය කරයි', 'பின்தொடர்கிறது'),
  K.stopFollowing: (
    'Stop following',
    'අනුගමනය නවත්වන්න',
    'பின்தொடர்வதை நிறுத்து',
  ),
  K.foodMenu: ('Food menu', 'ආහාර මෙනුව', 'உணவு மெனு'),
  K.foodMenuAt: ('At {station}', '{station} හි', '{station} இல்'),
  K.menuFrom: ('Menu from {time}', 'මෙනුව {time} සිට', 'மெனு {time} முதல்'),
  K.menuMayHaveChanged: (
    'It may have changed.',
    'එය වෙනස් වී තිබිය හැක.',
    'இது மாறியிருக்கலாம்.',
  ),
  K.available: ('Available', 'ඇත', 'கிடைக்கும்'),
  K.soldOut: ('Sold out', 'අවසන්', 'விற்றுத் தீர்ந்தது'),
  K.longStop: ('Long stop', 'දිගු නැවතුම', 'நீண்ட நிறுத்தம்'),
  K.orderOnWebsite: (
    "Order on the caterer's website",
    'ආහාර සපයන්නාගේ වෙබ් අඩවියෙන් ඇණවුම් කරන්න',
    'உணவு வழங்குநரின் இணையதளத்தில் ஆர்டர் செய்யுங்கள்',
  ),
  K.orderNote: (
    "Ordering, payment and collection happen on the caterer's website. SmartRail does not see your order.",
    'ඇණවුම, ගෙවීම සහ ලබාගැනීම ආහාර සපයන්නාගේ වෙබ් අඩවියේ සිදුවේ. SmartRail ඔබේ ඇණවුම නොදකී.',
    'ஆர்டர், கட்டணம் மற்றும் பெறுதல் உணவு வழங்குநரின் இணையதளத்தில் நடக்கும். உங்கள் ஆர்டரை SmartRail பார்ப்பதில்லை.',
  ),
  K.orderOffline: (
    "You're offline. Connect to open the caterer's website.",
    'ඔබ ඔෆ්ලයින්. වෙබ් අඩවිය විවෘත කිරීමට සම්බන්ධ වන්න.',
    'நீங்கள் ஆஃப்லைனில். இணையதளத்தைத் திறக்க இணைக்கவும்.',
  ),
  K.orderFailed: (
    'The website could not be opened.',
    'වෙබ් අඩවිය විවෘත කළ නොහැකි විය.',
    'இணையதளத்தைத் திறக்க முடியவில்லை.',
  ),
  K.price: ('Rs {price}', 'රු. {price}', 'ரூ. {price}'),
  K.notFoundTitle: (
    'No journey found for this train today',
    'මෙම දුම්රිය සඳහා අද ගමනක් හමු නොවීය',
    'இந்த ரயிலுக்கு இன்று பயணம் இல்லை',
  ),
  K.notFoundBody: (
    'Check the train number or try again later.',
    'දුම්රිය අංකය පරීක්ෂා කරන්න හෝ පසුව නැවත උත්සාහ කරන්න.',
    'ரயில் எண்ணைச் சரிபார்க்கவும் அல்லது பின்னர் முயற்சிக்கவும்.',
  ),
  K.arrivedBody: (
    'This train has finished its journey.',
    'මෙම දුම්රිය ගමන අවසන් කර ඇත.',
    'இந்த ரயில் தன் பயணத்தை முடித்துவிட்டது.',
  ),
  K.alertsTitle: ('Service alerts', 'සේවා දැනුම්දීම්', 'சேவை அறிவிப்புகள்'),
  K.trainsYouFollow: (
    'Trains you follow',
    'ඔබ අනුගමනය කරන දුම්රිය',
    'நீங்கள் பின்தொடரும் ரயில்கள்',
  ),
  K.noFollowed: (
    'You are not following any train.',
    'ඔබ කිසිදු දුම්රියක් අනුගමනය නොකරයි.',
    'நீங்கள் எந்த ரயிலையும் பின்தொடரவில்லை.',
  ),
  K.noAlerts: (
    'No service alerts',
    'සේවා දැනුම්දීම් නැත',
    'சேவை அறிவிப்புகள் இல்லை',
  ),
  K.settingsTitle: ('Settings', 'සැකසුම්', 'அமைப்புகள்'),
  K.language: ('Language', 'භාෂාව', 'மொழி'),
  K.followedTrains: (
    'Followed trains',
    'අනුගමනය කරන දුම්රිය',
    'பின்தொடரும் ரயில்கள்',
  ),
  K.remove: ('Remove', 'ඉවත් කරන්න', 'நீக்கு'),
  K.privacyTitle: ('Privacy', 'පෞද්ගලිකත්වය', 'தனியுரிமை'),
  K.privacyBody: (
    'There is no account. Your language, starting station and followed trains stay on this phone. No location is used.',
    'ගිණුමක් නැත. ඔබේ භාෂාව, ආරම්භක ස්ථානය සහ අනුගමනය කරන දුම්රිය මෙම දුරකථනයේම පවතී. ස්ථානය භාවිතා නොකරයි.',
    'கணக்கு இல்லை. உங்கள் மொழி, தொடக்க நிலையம், பின்தொடரும் ரயில்கள் இந்தத் தொலைபேசியிலேயே இருக்கும். இருப்பிடம் பயன்படுத்தப்படாது.',
  ),
  K.ticketsTitle: ('My tickets', 'මගේ ටිකට්', 'என் டிக்கெட்டுகள்'),
  K.ticketsBody: (
    'Tickets are not available in this version.',
    'මෙම අනුවාදයේ ටිකට් නොමැත.',
    'இந்தப் பதிப்பில் டிக்கெட்டுகள் இல்லை.',
  ),
  K.connectOnce: (
    'Connect once to load the stations',
    'ස්ථාන පූරණය කිරීමට එක් වරක් සම්බන්ධ වන්න',
    'நிலையங்களை ஏற்ற ஒருமுறை இணையுங்கள்',
  ),
  K.retry: ('Retry', 'නැවත උත්සාහ කරන්න', 'மீண்டும் முயற்சி'),
  K.back: ('Back', 'ආපසු', 'பின்'),
  K.trainsToday: ('Trains today', 'අද දුම්රිය', 'இன்றைய ரயில்கள்'),
};

/// The words of one language. `strings[K.x]` for plain text, [fmt] to fill in `{name}` values.
class Strings {
  const Strings(this.lang);

  final Lang lang;

  String operator [](K key) {
    final (en, si, ta) = _table[key]!;
    return switch (lang) {
      Lang.en => en,
      Lang.si =>
        si.isEmpty
            ? en
            : si, // a missing translation falls back to English (EC-78)
      Lang.ta => ta.isEmpty ? en : ta,
    };
  }

  String fmt(K key, Map<String, Object> values) {
    var text = this[key];
    values.forEach(
      (name, value) => text = text.replaceAll('{$name}', '$value'),
    );
    return text;
  }

  /// For the completeness test: the English, Sinhala and Tamil text of every key.
  static Iterable<MapEntry<K, (String, String, String)>> get all =>
      _table.entries;
}
