/**
 * Every word a station display shows, in English, Sinhala and Tamil side by side (D-19, FR-83), in one file.
 *
 * UNVERIFIED: only the phrases marked "canvas" below were taken from the Claude Design boards. Every other Sinhala and Tamil text was
 * written for this slice and has NOT been checked by a native speaker (NFR-34, EC-117). They are placeholders until it is. Times, train
 * numbers, platform numbers and icons are the same in all three languages and are not here.
 */
export interface Tri {
  en: string
  si: string
  ta: string
}

const tri = (en: string, si: string, ta: string): Tri => ({ en, si, ta })

/**
 * A length of time in the three languages, in hours and minutes once it is an hour or more (240 is "4 h", 65 is "1 h 05 min").
 * UNVERIFIED like the rest: the hour words (පැය, மணி) were written for this slice, the minute words are the canvas's.
 */
function duration(total: number): Tri {
  const minutes = Math.max(0, Math.round(total))
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  if (hours === 0) {
    return tri(`${minutes} min`, `මිනිත්තු ${minutes}ක්`, `${minutes} நிமிடம்`)
  }

  if (rest === 0) {
    return tri(`${hours} h`, `පැය ${hours}ක්`, `${hours} மணி`)
  }

  return tri(`${hours} h ${String(rest).padStart(2, '0')} min`, `පැය ${hours}ක් මිනිත්තු ${rest}ක්`, `${hours} மணி ${rest} நிமிடம்`)
}

export const d = {
  platform: tri('Platform', 'වේදිකාව', 'நடைமேடை'),
  train: tri('Train', 'දුම්රිය', 'ரயில்'),

  expectedArrival: tri('Expected arrival', 'අපේක්ෂිත පැමිණීම', 'எதிர்பார்க்கப்படும் வருகை'), // canvas
  expectedDeparture: tri('Expected departure', 'අපේක්ෂිත පිටත්වීම', 'எதிர்பார்க்கப்படும் புறப்பாடு'),
  lastKnownArrival: tri('Last known arrival', 'අවසන් දන්නා පැමිණීම', 'கடைசியாக அறியப்பட்ட வருகை'),
  lastKnownDeparture: tri('Last known departure', 'අවසන් දන්නා පිටත්වීම', 'கடைசியாக அறியப்பட்ட புறப்பாடு'),
  scheduledArrival: tri('Scheduled arrival', 'නියමිත පැමිණීම', 'திட்டமிட்ட வருகை'),
  scheduledDeparture: tri('Scheduled departure', 'නියමිත පිටත්වීම', 'திட்டமிட்ட புறப்பாடு'),

  onTime: tri('On time', 'නියමිත වේලාවට', 'சரியான நேரத்தில்'),
  scheduled: tri('Scheduled', 'කාලසටහනට අනුව', 'அட்டவணைப்படி'),
  atPlatform: tri('At platform', 'වේදිකාවේ ඇත', 'நடைமேடையில் உள்ளது'),
  departs: tri('Departs', 'පිටත් වේ', 'புறப்படும்'),
  waiting: tri('Waiting for update', 'යාවත්කාලීන කිරීමක් බලාපොරොත්තුවෙන්', 'புதுப்பிப்புக்காகக் காத்திருக்கிறது'),
  noNews: tri('No news yet', 'තොරතුරු නැත', 'செய்தி இல்லை'), // canvas
  lastSeen: tri('Last seen', 'අවසන් වරට දුටුවේ', 'கடைசியாகப் பார்த்தது'),
  departedFrom: tri('Departed', 'පිටත් විය', 'புறப்பட்டது'),
  arrivedAt: tri('Arrived', 'පැමිණියේය', 'வந்தடைந்தது'),

  delayed: (n: number): Tri => {
    const length = duration(n)
    return tri(`Delayed ${length.en}`, `${length.si} ප්‍රමාදයි`, `${length.ta} தாமதம்`)
  },
  aboutLate: (n: number): Tri => {
    const length = duration(n)
    return tri(`About ${length.en} late`, `${length.si} පමණ ප්‍රමාදයි`, `சுமார் ${length.ta} தாமதம்`) // canvas (with 10 min)
  },

  nextTrains: tri('Next trains', 'ඊළඟ දුම්රිය', 'அடுத்த ரயில்கள்'),

  lastUpdate: tri('Last update', 'අවසන් යාවත්කාලීනය', 'கடைசிப் புதுப்பிப்பு'),
  updated: tri('Updated', 'යාවත්කාලීන කළේ', 'புதுப்பிக்கப்பட்டது'),
  liveUnavailable: tri('Live updates unavailable', 'සජීවී යාවත්කාලීන නොමැත', 'நேரடி புதுப்பிப்புகள் இல்லை'), // canvas
  notLive: tri('Not live', 'සජීවී නොවේ', 'நேரலை இல்லை'),
  connecting: tri('Waiting for the first update', 'පළමු යාවත්කාලීනය බලාපොරොත්තුවෙන්', 'முதல் புதுப்பிப்புக்காகக் காத்திருக்கிறது'),

  noTrainsUntil: (time: string): Tri =>
    tri(`No trains until ${time}`, `${time} දක්වා දුම්රිය නැත`, `${time} வரை ரயில்கள் இல்லை`),
  nextService: tri('Next service', 'ඊළඟ සේවාව', 'அடுத்த சேவை'),
  noMoreToday: tri('No more services today', 'අද තවත් සේවාවන් නැත', 'இன்று மேலும் சேவைகள் இல்லை'),
  firstTomorrow: (time: string): Tri =>
    tri(`First service tomorrow ${time}`, `හෙට පළමු සේවාව ${time}`, `நாளை முதல் சேவை ${time}`),
  firstService: tri('First service', 'පළමු සේවාව', 'முதல் சேவை'),
  notSetUp: tri('This display is not set up', 'මෙම සංදර්ශකය සකසා නැත', 'இந்தத் திரை அமைக்கப்படவில்லை'),

  columns: {
    time: tri('Time', 'වේලාව', 'நேரம்'),
    destination: tri('Destination', 'ගමනාන්තය', 'சேருமிடம்'),
    platform: tri('Platform', 'වේදිකාව', 'நடைமேடை'),
    status: tri('Status', 'තත්ත්වය', 'நிலை'),
  },
}

/** What a text function is called with in the completeness test. */
const samples: Record<string, number | string> = { delayed: 7, aboutLate: 7, noTrainsUntil: '09:25', firstTomorrow: '05:30' }

/** Every text in the table with the path it is at, so a test can prove none is missing a language (or a placeholder in one of them). */
export function allTexts(): { path: string; text: Tri }[] {
  const out: { path: string; text: Tri }[] = []
  const visit = (path: string, key: string, value: unknown) => {
    if (typeof value === 'function') {
      out.push({ path, text: (value as (arg: number | string) => Tri)(samples[key] ?? 0) })
    } else if (value && typeof value === 'object' && 'en' in value && 'si' in value && 'ta' in value) {
      out.push({ path, text: value as Tri })
    } else if (value && typeof value === 'object') {
      Object.entries(value).forEach(([inner, next]) => visit(path ? `${path}.${inner}` : inner, inner, next))
    }
  }
  visit('', '', d)
  return out
}
