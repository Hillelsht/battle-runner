namespace BattleRunner.Core.Text
{
    /// <summary>
    /// The three tables, authored by hand.
    ///
    /// HOW TO READ AN ENTRY. `{0}`, `{1}` are values filled at runtime and must survive
    /// translation — the tests check that. A `|` separates plural variants: two for English and
    /// Hebrew (one, other), three for Russian (one, few, many), and <see cref="Plural"/> explains
    /// why Russian's are not a ternary.
    ///
    /// THE NUMBERS IN A STRING ARE LOAD-BEARING. Several of these quote a balance value that also
    /// exists as a StatModifier somewhere else, and a translation that mistyped one would state a
    /// rule the game does not follow. Every translation's digits are checked against the English
    /// source's, so a wrong number fails the build rather than misleading a player.
    ///
    /// HEBREW IS WRITTEN IN LOGICAL ORDER and without nikud. Vowel points are combining marks,
    /// and reordering a string for display puts a combining mark before the letter it belongs to;
    /// modern Hebrew interfaces do not use them anyway. A test enforces their absence.
    ///
    /// THE REGISTER: informal, and phrased so it never assumes the player's gender. Russian uses
    /// ты and imperatives, which are genderless. Hebrew imperatives are NOT, so instructions are
    /// phrased as infinitives — "להחליק את האגודל" rather than a masculine "החלק".
    /// </summary>
    public static class LocTables
    {
        public static readonly (LocKey Key, string Text)[] English =
        {
            // ---- the tutorial ---------------------------------------------------------
            (LocKey.TutorialSteerTitle, "DRAG TO STEER"),
            (LocKey.TutorialSteerDetail, "Slide your thumb left or right"),
            (LocKey.TutorialGateTitle, "TAKE THE BIGGER GATE"),
            (LocKey.TutorialGateDetail, "Blue adds, gold multiplies, red takes"),
            (LocKey.TutorialCastTitle, "FLICK UP TO CAST"),
            (LocKey.TutorialCastDetail, "Lift your thumb, then swipe up fast"),
            (LocKey.TutorialBlockTitle, "FLICK DOWN TO BLOCK"),
            (LocKey.TutorialBlockDetail, "Lift your thumb, then swipe down fast"),

            // ---- the four heroes ------------------------------------------------------
            (LocKey.HeroWardenName, "WARDEN"),
            (LocKey.HeroWardenTagline, "Holds the line. Two shields, and a blocked ambush joins you."),
            (LocKey.HeroAshcallerName, "ASHCALLER"),
            (LocKey.HeroAshcallerTagline, "Burns the road ahead. Two spells, and they reach further."),
            (LocKey.HeroHoundmasterName, "HOUNDMASTER"),
            (LocKey.HeroHoundmasterTagline, "Two hounds at the flank. Every recruit gate pays more."),
            (LocKey.HeroRevenantName, "REVENANT"),
            (LocKey.HeroRevenantTagline, "The fallen come back. What you lose, you mostly get again."),

            // ---- the eight worlds -----------------------------------------------------
            (LocKey.WorldAshenRoad, "The Ashen Road"),
            (LocKey.WorldThistlewood, "Thistlewood"),
            (LocKey.WorldSunkenCrypt, "The Sunken Crypt"),
            (LocKey.WorldEmberFields, "Ember Fields"),
            (LocKey.WorldBoneWastes, "The Bone Wastes"),
            (LocKey.WorldFrozenReach, "The Frozen Reach"),
            (LocKey.WorldBloodMarsh, "The Blood Marsh"),
            (LocKey.WorldThroneOfDust, "The Throne of Dust"),

            // ---- boss affixes ---------------------------------------------------------
            (LocKey.AffixFrenzied, "Frenzied"),
            (LocKey.AffixArmoured, "Armoured"),
            (LocKey.AffixVampiric, "Vampiric"),
            (LocKey.AffixHaunted, "Haunted"),
            (LocKey.AffixColossal, "Colossal"),
            (LocKey.AffixDecorate, "{0} {1}"),

            // ---- paragon --------------------------------------------------------------
            (LocKey.ParagonMight, "Might"),
            (LocKey.ParagonVigor, "Vigor"),
            (LocKey.ParagonGates, "Gates"),
            (LocKey.ParagonFortune, "Fortune"),
            (LocKey.ParagonFocus, "Focus"),
            (LocKey.ParagonSpeed, "Speed"),
            (LocKey.ParagonUnknownPath, "Unknown path"),
            (LocKey.ParagonNeedKeystone, "Reach a keystone first"),
            (LocKey.ParagonCostsPoints, "Costs {0} point|Costs {0} points"),

            // ---- save slots -----------------------------------------------------------
            (LocKey.SlotNumber, "SLOT {0}"),
            (LocKey.SlotNewGame, "New game"),
            (LocKey.SlotLevel, "Level {0}"),
            (LocKey.SlotTalents, "{0} talent|{0} talents"),
            (LocKey.SlotUnspent, "{0} unspent")
        };

        public static readonly (LocKey Key, string Text)[] Russian =
        {
            // ---- the tutorial ---------------------------------------------------------
            (LocKey.TutorialSteerTitle, "ВЕДИ ПАЛЬЦЕМ"),
            (LocKey.TutorialSteerDetail, "Веди большой палец влево или вправо"),
            (LocKey.TutorialGateTitle, "БЕРИ ВОРОТА ПОБОЛЬШЕ"),
            (LocKey.TutorialGateDetail, "Синие прибавляют, золотые умножают, красные отнимают"),
            (LocKey.TutorialCastTitle, "ВЗМАХ ВВЕРХ — ЗАКЛИНАНИЕ"),
            (LocKey.TutorialCastDetail, "Оторви палец, затем резко проведи вверх"),
            (LocKey.TutorialBlockTitle, "ВЗМАХ ВНИЗ — ЩИТ"),
            (LocKey.TutorialBlockDetail, "Оторви палец, затем резко проведи вниз"),

            // ---- the four heroes ------------------------------------------------------
            (LocKey.HeroWardenName, "СТРАЖ"),
            (LocKey.HeroWardenTagline, "Держит строй. Два щита, и отражённая засада встаёт в ряды."),
            (LocKey.HeroAshcallerName, "ПЕПЛОЗВАТЕЦ"),
            (LocKey.HeroAshcallerTagline, "Выжигает путь впереди. Два заклинания, и бьют они дальше."),
            (LocKey.HeroHoundmasterName, "ПСАРЬ"),
            (LocKey.HeroHoundmasterTagline, "Два пса на фланге. Любые ворота набора дают больше."),
            (LocKey.HeroRevenantName, "ВОССТАВШИЙ"),
            (LocKey.HeroRevenantTagline, "Павшие возвращаются. Потерянное почти всё вернётся."),

            // ---- the eight worlds -----------------------------------------------------
            (LocKey.WorldAshenRoad, "Пепельный тракт"),
            (LocKey.WorldThistlewood, "Чертополошье"),
            (LocKey.WorldSunkenCrypt, "Затонувший склеп"),
            (LocKey.WorldEmberFields, "Тлеющие поля"),
            (LocKey.WorldBoneWastes, "Костяные пустоши"),
            (LocKey.WorldFrozenReach, "Стылый предел"),
            (LocKey.WorldBloodMarsh, "Кровавая топь"),
            (LocKey.WorldThroneOfDust, "Престол праха"),

            // ---- boss affixes ---------------------------------------------------------
            // Masculine forms throughout, which is not an accident: every boss name in the
            // Russian table is a masculine noun, including the Hollow Leech — rendered as
            // "Кровосос" rather than the feminine "Пиявка" precisely so one adjective form
            // serves all six. Russian adjectives agree in gender, and a table with two forms
            // per affix would need a gender field on every boss to pick between them.
            (LocKey.AffixFrenzied, "Яростный"),
            (LocKey.AffixArmoured, "Бронированный"),
            (LocKey.AffixVampiric, "Вампирический"),
            (LocKey.AffixHaunted, "Одержимый"),
            (LocKey.AffixColossal, "Колоссальный"),
            (LocKey.AffixDecorate, "{0} {1}"),

            // ---- paragon --------------------------------------------------------------
            (LocKey.ParagonMight, "Мощь"),
            (LocKey.ParagonVigor, "Живучесть"),
            (LocKey.ParagonGates, "Ворота"),
            (LocKey.ParagonFortune, "Удача"),
            (LocKey.ParagonFocus, "Сосредоточение"),
            (LocKey.ParagonSpeed, "Скорость"),
            (LocKey.ParagonUnknownPath, "Неизвестный путь"),
            (LocKey.ParagonNeedKeystone, "Сначала возьми краеугольный талант"),
            (LocKey.ParagonCostsPoints, "Стоит {0} очко|Стоит {0} очка|Стоит {0} очков"),

            // ---- save slots -----------------------------------------------------------
            (LocKey.SlotNumber, "ЯЧЕЙКА {0}"),
            (LocKey.SlotNewGame, "Новая игра"),
            (LocKey.SlotLevel, "Уровень {0}"),
            (LocKey.SlotTalents, "{0} талант|{0} таланта|{0} талантов"),
            (LocKey.SlotUnspent, "{0} не вложено")
        };

        public static readonly (LocKey Key, string Text)[] Hebrew =
        {
            // ---- the tutorial ---------------------------------------------------------
            // Infinitives, not imperatives. Hebrew imperatives carry gender — החלק to a man,
            // החליקי to a woman — and the game does not know which it is talking to.
            (LocKey.TutorialSteerTitle, "להחליק כדי לנווט"),
            (LocKey.TutorialSteerDetail, "להחליק את האגודל שמאלה או ימינה"),
            (LocKey.TutorialGateTitle, "לבחור בשער הגדול"),
            (LocKey.TutorialGateDetail, "כחול מוסיף, זהב מכפיל, אדום לוקח"),
            (LocKey.TutorialCastTitle, "תנועה מהירה למעלה - לחש"),
            (LocKey.TutorialCastDetail, "להרים את האגודל ואז להחליק מהר למעלה"),
            (LocKey.TutorialBlockTitle, "תנועה מהירה למטה - מגן"),
            (LocKey.TutorialBlockDetail, "להרים את האגודל ואז להחליק מהר למטה"),

            // ---- the four heroes ------------------------------------------------------
            (LocKey.HeroWardenName, "השומר"),
            (LocKey.HeroWardenTagline, "מחזיק את הקו. שני מגנים, ומארב שנחסם מצטרף אליך."),
            (LocKey.HeroAshcallerName, "קורא האפר"),
            (LocKey.HeroAshcallerTagline, "שורף את הדרך שלפניו. שני לחשים, והם מגיעים רחוק יותר."),
            (LocKey.HeroHoundmasterName, "אדון הכלבים"),
            (LocKey.HeroHoundmasterTagline, "שני כלבים באגף. כל שער גיוס משלם יותר."),
            (LocKey.HeroRevenantName, "השב"),
            (LocKey.HeroRevenantTagline, "הנופלים חוזרים. את מה שאיבדת תקבל ברובו בחזרה."),

            // ---- the eight worlds -----------------------------------------------------
            (LocKey.WorldAshenRoad, "דרך האפר"),
            (LocKey.WorldThistlewood, "יער הדרדר"),
            (LocKey.WorldSunkenCrypt, "הכוך הטבוע"),
            (LocKey.WorldEmberFields, "שדות הגחלים"),
            (LocKey.WorldBoneWastes, "שממות העצם"),
            (LocKey.WorldFrozenReach, "המרחב הקפוא"),
            (LocKey.WorldBloodMarsh, "ביצת הדם"),
            (LocKey.WorldThroneOfDust, "כס האבק"),

            // ---- boss affixes ---------------------------------------------------------
            (LocKey.AffixFrenzied, "המשתולל"),
            (LocKey.AffixArmoured, "המשוריין"),
            (LocKey.AffixVampiric, "מוצץ הדם"),
            (LocKey.AffixHaunted, "הרדוף"),
            (LocKey.AffixColossal, "הענק"),
            // REVERSED, and this is the reason the pairing is a template at all: a Hebrew
            // adjective follows the noun it describes, so the boss's name comes first.
            (LocKey.AffixDecorate, "{1} {0}"),

            // ---- paragon --------------------------------------------------------------
            (LocKey.ParagonMight, "עוצמה"),
            (LocKey.ParagonVigor, "חוסן"),
            (LocKey.ParagonGates, "שערים"),
            (LocKey.ParagonFortune, "מזל"),
            (LocKey.ParagonFocus, "ריכוז"),
            (LocKey.ParagonSpeed, "מהירות"),
            (LocKey.ParagonUnknownPath, "נתיב לא מוכר"),
            (LocKey.ParagonNeedKeystone, "קודם צריך להגיע לאבן פינה"),
            (LocKey.ParagonCostsPoints, "עולה נקודה אחת|עולה {0} נקודות"),

            // ---- save slots -----------------------------------------------------------
            (LocKey.SlotNumber, "משבצת {0}"),
            (LocKey.SlotNewGame, "משחק חדש"),
            (LocKey.SlotLevel, "שלב {0}"),
            (LocKey.SlotTalents, "כישרון אחד|{0} כישרונות"),
            (LocKey.SlotUnspent, "{0} לא הושקעו")
        };
    }
}
