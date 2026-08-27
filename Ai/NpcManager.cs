using System;
using System.Collections.Generic;

namespace gta.Ai
{
    public class NpcManager
    {
        private readonly Dictionary<int, NpcIdentity> _identities = new Dictionary<int, NpcIdentity>();
        private readonly AiSettings _settings;
        private readonly NpcMemoryStore _memory = new NpcMemoryStore();
        private readonly Random _random = new Random();

        private static readonly string[] MaleFirstNames = { "John", "Mike", "Carlos", "David", "Tom", "Jerry", "Robert", "James", "William", "Michael", "Richard", "Thomas", "Charles", "Christopher", "Daniel" };
        private static readonly string[] FemaleFirstNames = { "Sarah", "Emily", "Anna", "Maria", "Jessica", "Ashley", "Taylor", "Sophia", "Isabella", "Emma", "Olivia", "Ava", "Abigail", "Madison", "Chloe" };
        private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
        private static readonly string[] Professions = { "Mechanic", "Doctor", "Unemployed", "Gangster", "Businessman", "Student", "Teacher", "Cashier", "Cop", "Taxi Driver" };
        private static readonly string[] Personalities = { "Angry and tired", "Happy and naive", "Cowardly and nervous", "Aggressive and rude", "Polite and helpful", "Sarcastic and bored", "Drunk and confused" };

        public NpcManager(AiSettings settings)
        {
            _settings = settings;
        }

        public NpcIdentity GetOrCreateIdentity(GTA.Ped ped)
        {
            var handle = ped.Handle;
            if (_identities.TryGetValue(handle, out var identity))
            {
                return identity;
            }

            // Известных персонажей (Майкл/Франклин/Тревор) распознаём по модели и даём каноничную личность
            var newIdentity = TryCreateKnownCharacterIdentity(ped) ?? GenerateRandomIdentity(ped);
            _identities[handle] = newIdentity;
            return newIdentity;
        }

        // Если пед — известный персонаж GTA V (по модели), выдаём ему настоящую личность вместо случайной.
        private NpcIdentity TryCreateKnownCharacterIdentity(GTA.Ped ped)
        {
            var pedModel = ped.Model;
            foreach (var kc in KnownCharacters)
            {
                if (pedModel != new GTA.Model(kc.Model)) continue;

                var provider = _settings.GetProvider(_settings.ActiveProvider);
                var voices = kc.IsMale ? provider.MaleVoiceIds : provider.FemaleVoiceIds;
                if (voices == null || voices.Length == 0) voices = provider.VoiceIds;
                var voiceId = (voices != null && voices.Length > 0) ? voices[kc.VoicePick % voices.Length] : "";

                // Подтягиваем долговременную память по ключу модели (переживает рестарт)
                var mem = _memory.Get(kc.Model);
                var identity = new NpcIdentity
                {
                    Handle = ped.Handle,
                    Name = kc.Name,
                    Profession = kc.Profession,
                    Personality = kc.Personality,
                    VoiceId = voiceId,
                    IsKnownCharacter = true,
                    ModelKey = kc.Model,
                    Summary = mem.Summary,
                    MaxRecent = 40
                };
                identity.ChatHistory.AddRange(mem.Recent);
                return identity;
            }
            return null;
        }

        private sealed class KnownCharacter
        {
            public string Model;
            public string Name;
            public string Profession;
            public string Personality;
            public bool IsMale;
            public int VoicePick;
        }

        // Таблица известных персонажей. Добавить нового = одна строка (модель + бриф).
        private static readonly KnownCharacter[] KnownCharacters =
        {
            new KnownCharacter { Model = "player_zero", IsMale = true, VoicePick = 0, Name = "Michael De Santa",
                Profession = "Retired bank robber (real name Michael Townley), now in witness protection in a Rockford Hills mansion",
                Personality = "Cynical, world-weary and sarcastic with dry, deadpan humor. Middle-aged, depressed and in therapy, secretly missing his glory days robbing banks. Constantly stressed by his dysfunctional family: nagging wife Amanda, lazy son Jimmy, wild daughter Tracey. Short-tempered but tired — he sighs, complains, drops references to his therapist, money and 'the old days', and quotes classic Vinewood movies. Talks like a jaded ex-pro who's seen it all." },

            new KnownCharacter { Model = "player_one", IsMale = true, VoicePick = 1, Name = "Franklin Clinton",
                Profession = "Hustler and former repo man from Forum Drive, South Los Santos",
                Personality = "Calm, sharp and ambitious with a laid-back street manner. Grew up in the hood raised by his Aunt Denise, desperate to leave gangbanging behind and earn real money. Pragmatic, loyal and level-headed — rarely loses his cool. Speaks casual LS slang ('homie', 'dawg', 'for real'), business-minded, looks up to Michael as a mentor. Has a loyal dog named Chop." },

            new KnownCharacter { Model = "player_two", IsMale = true, VoicePick = 2, Name = "Trevor Philips",
                Profession = "Unhinged boss of Trevor Philips Enterprises (meth and guns) out of Sandy Shores, Blaine County; ex-military pilot",
                Personality = "Violently unstable, impulsive, crude and unpredictable — swings from menacing rage to bizarre affection within a single sentence. Canadian, loud, profane and darkly funny. Fiercely and possessively loyal to the very few people he loves, especially Michael, whose faked 'death' he's still furious about. Rants about meth, chaos, Blaine County and his crew Ron and Wade. Equal parts terrifying and weirdly sentimental; never calm, never polite." },

            new KnownCharacter { Model = "ig_lestercrest", IsMale = true, VoicePick = 3, Name = "Lester Crest",
                Profession = "Genius hacker, fixer and heist mastermind",
                Personality = "Brilliant but physically frail criminal genius who plans the crew's heists from the shadows. Sarcastic, paranoid and socially awkward, obsessed with the stock market and conspiracy theories. Chronically ill — wheezes and complains about his health — but razor-sharp, condescending and always three steps ahead." },

            new KnownCharacter { Model = "ig_lamardavis", IsMale = true, VoicePick = 0, Name = "Lamar Davis",
                Profession = "Gangbanger and hustler from the Families, Franklin's best friend",
                Personality = "Loud, hilarious and impulsive hood schemer, Franklin's ride-or-die best friend. Cracks jokes and roasts everyone non-stop (especially Franklin), full of half-baked get-rich-quick plans. Loyal under the bravado, street-smart but reckless, always hyped up." },

            new KnownCharacter { Model = "ig_amandatownley", IsMale = false, VoicePick = 0, Name = "Amanda De Santa",
                Profession = "Michael's wife, former stripper, Rockford Hills socialite",
                Personality = "Sharp-tongued, materialistic and bored housewife. A former stripper who now spends Michael's money on shopping, tennis, yoga and the occasional affair. Nags and argues with Michael, dramatic and defensive — but deep down still loves her messed-up family." },

            new KnownCharacter { Model = "ig_jimmydisanto", IsMale = true, VoicePick = 1, Name = "Jimmy De Santa",
                Profession = "Michael's unemployed adult son",
                Personality = "Lazy, overweight stoner gamer who still lives off his parents. Whiny, entitled and immature, always scheming for quick cash or weed, glued to his console and energy drinks — but occasionally shows he actually means well." },

            new KnownCharacter { Model = "ig_tracydisanto", IsMale = false, VoicePick = 1, Name = "Tracey De Santa",
                Profession = "Michael's daughter, aspiring dancer and reality-TV wannabe",
                Personality = "Naive, dramatic fame-chaser who dreams of being a dancer or reality star. Parties hard, ditzy and self-absorbed, bickers with her dad — but spirited and good-hearted under the airhead act." },

            new KnownCharacter { Model = "ig_ron", IsMale = true, VoicePick = 2, Name = "Ron Jakowski",
                Profession = "Trevor's paranoid business partner in Sandy Shores",
                Personality = "Jittery, cowardly conspiracy nut who helps run Trevor's operations. Obsessed with government plots, aliens and surveillance, terrified of Trevor yet doggedly loyal, mumbles nervously and rants about the end of the world." },

            new KnownCharacter { Model = "ig_wade", IsMale = true, VoicePick = 3, Name = "Wade Hebert",
                Profession = "Trevor's dim-witted juggalo follower",
                Personality = "Slow, naive and harmless juggalo who trails after Trevor. Easily confused, speaks simply and childishly, gets scared and overwhelmed, and mostly just wants to find his cousin Floyd and listen to clown music." },

            new KnownCharacter { Model = "ig_devin", IsMale = true, VoicePick = 0, Name = "Devin Weston",
                Profession = "Self-made billionaire venture capitalist",
                Personality = "Smug, arrogant billionaire who sees people as disposable assets. Manipulative and smarmy, lectures everyone about being 'self-made' and the philosophy of wealth and power, utterly without empathy behind a polished smile." },

            new KnownCharacter { Model = "ig_davenorton", IsMale = true, VoicePick = 1, Name = "Dave Norton",
                Profession = "FIB agent, Michael's longtime handler",
                Personality = "Stressed, pragmatic FIB agent forever cleaning up Michael's and the crew's messes. Corrupt but conflicted, weary and exasperated, talks like a cop who's in way too deep and just wants the chaos to stop." },

            new KnownCharacter { Model = "ig_stevehaines", IsMale = true, VoicePick = 2, Name = "Steve Haines",
                Profession = "Corrupt FIB agent and TV personality",
                Personality = "Loud, aggressive, image-obsessed FIB agent who hosts his own TV show. Self-serving, bullying and manipulative, treats everyone as expendable and flies into power-tripping rages while obsessing over his public image." },

            new KnownCharacter { Model = "ig_siemonyetarian", IsMale = true, VoicePick = 3, Name = "Simeon Yetarian",
                Profession = "Armenian luxury car dealership owner",
                Personality = "Smarmy, manipulative car dealer who exploits and guilt-trips his employees while preaching about 'the American Dream'. Two-faced and sanctimonious, flips from oily charm to threats in an instant." },

            new KnownCharacter { Model = "ig_lazlow", IsMale = true, VoicePick = 0, Name = "Lazlow Jones",
                Profession = "Washed-up radio and TV host",
                Personality = "Sleazy, cringe-worthy washed-up media personality desperate to stay relevant. Self-promoting, awkward and out of touch, name-drops constantly and humiliates himself chasing fame." },

            new KnownCharacter { Model = "ig_denise", IsMale = false, VoicePick = 2, Name = "Denise Clinton",
                Profession = "Franklin's outspoken aunt",
                Personality = "Loud, opinionated new-age aunt who raised Franklin and still bosses him around. Spouts self-help and empowerment slogans, nosy and combative — but family at heart." },

            new KnownCharacter { Model = "ig_floyd", IsMale = true, VoicePick = 1, Name = "Floyd Hebert",
                Profession = "Meek longshoreman, Wade's cousin",
                Personality = "Timid, miserable longshoreman bullied into letting Trevor crash at his apartment. Anxious, submissive and soft-spoken, constantly apologizing and dreading whatever Trevor will do next." },
        };

        // Удаляет личности педов, которых уже нет в мире (handle освободился/переиспользован),
        // чтобы словарь не рос бесконечно и не плодил коллизии handle. Вызывать из главного потока.
        public void CleanupDeadIdentities()
        {
            if (_identities.Count == 0) return;

            List<int> dead = null;
            foreach (var handle in _identities.Keys)
            {
                var entity = GTA.Entity.FromHandle(handle);
                if (entity == null || !entity.Exists())
                {
                    (dead ?? (dead = new List<int>())).Add(handle);
                }
            }

            if (dead == null) return;
            foreach (var handle in dead)
            {
                _identities.Remove(handle);
            }
        }

        // Сохраняет долговременную память известного персонажа на диск (по ключу модели).
        public void PersistKnownCharacter(NpcIdentity identity)
        {
            if (identity == null || string.IsNullOrEmpty(identity.ModelKey)) return;
            _memory.Save(identity.ModelKey, identity.Summary, identity.ChatHistory);
        }

        private NpcIdentity GenerateRandomIdentity(GTA.Ped ped)
        {
            var isMale = GTA.Native.Function.Call<bool>(GTA.Native.Hash.IS_PED_MALE, ped);
            var firstName = isMale 
                ? MaleFirstNames[_random.Next(MaleFirstNames.Length)] 
                : FemaleFirstNames[_random.Next(FemaleFirstNames.Length)];
            var lastName = LastNames[_random.Next(LastNames.Length)];
            
            // Роль по типу педа — чтобы солдат/коп/гангстер не получал случайную гражданскую личность
            int pedType = GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_PED_TYPE, ped.Handle);
            bool isCop = pedType == 6 || pedType == 30;   // COP / SWAT
            bool isArmy = pedType == 32;                  // ARMY
            bool isGang = pedType >= 7 && pedType <= 18;  // банды

            string profession, personality;
            if (isArmy)
            {
                profession = "Soldier on duty at a military base";
                personality = "Disciplined, aggressive and on high alert; treats anyone who doesn't belong as a hostile intruder";
            }
            else if (isCop)
            {
                profession = "Police Officer";
                personality = "Strict, authoritative, and suspicious";
            }
            else if (isGang)
            {
                profession = "Street gang member";
                personality = "Aggressive, territorial and quick to violence";
            }
            else
            {
                profession = Professions[_random.Next(Professions.Length)];
                personality = Personalities[_random.Next(Personalities.Length)];
            }
            
            var voiceId = "";
            var activeTts = _settings.ActiveProvider;
            var provider = _settings.GetProvider(activeTts);
            
            var voiceList = isMale ? provider.MaleVoiceIds : provider.FemaleVoiceIds;
            if (voiceList == null || voiceList.Length == 0)
            {
                voiceList = provider.VoiceIds;
            }

            if (voiceList != null && voiceList.Length > 0)
            {
                voiceId = voiceList[_random.Next(voiceList.Length)];
            }

            return new NpcIdentity
            {
                Handle = ped.Handle,
                Name = $"{(isCop ? "Officer " : "")}{firstName} {lastName}",
                Profession = profession,
                Personality = personality,
                VoiceId = voiceId
            };
        }
    }
}
