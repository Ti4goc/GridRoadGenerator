using System.Collections.Generic;

namespace GridRoadGenerator.Localization
{
    /// <summary>
    /// Onglet Rotunda de la section Redes (motif Radial). Par langue, dans l'ordre de
    /// <see cref="Keys"/> : nom de l'onglet, puis sa description.
    /// </summary>
    public static class RoundaboutTranslations
    {
        public static readonly string[] Keys =
        {
            "GridRoadGenerator.UI.NetworkRoundabout",
            "GridRoadGenerator.UI.Tip.NetworkRoundabout",
        };

        private static Dictionary<string, string[]> s_All;

        public static Dictionary<string, string[]> All => s_All ?? (s_All = Build());

        public static void AddTo(Dictionary<string, string> entries, string locale)
        {
            if (!All.TryGetValue(locale, out string[] texts) || texts.Length != Keys.Length)
            {
                return;
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                entries[Keys[i]] = texts[i];
            }
        }

        private static Dictionary<string, string[]> Build() => new Dictionary<string, string[]>
        {
            ["en-US"] = new[] { "Roundabout", "Road type of the central roundabout. A one-way road is laid in the driving direction of your city. Auto uses the local street network." },
            ["pt-PT"] = new[] { "Rotunda", "Tipo de estrada da rotunda central. Uma estrada de sentido único é colocada no sentido de circulação da tua cidade. Auto usa a rede das ruas." },
            ["pt-BR"] = new[] { "Rotatória", "Tipo de via da rotatória central. Uma via de mão única é colocada no sentido de circulação da sua cidade. Auto usa a rede das ruas." },
            ["fr-FR"] = new[] { "Rond-point", "Type de route du rond-point central. Une route à sens unique est posée dans le sens de circulation de votre ville. Auto utilise le réseau des rues." },
            ["de-DE"] = new[] { "Kreisverkehr", "Straßentyp des zentralen Kreisverkehrs. Eine Einbahnstraße wird in Fahrtrichtung deiner Stadt verlegt. Auto verwendet das Straßennetz der Wohnstraßen." },
            ["es-ES"] = new[] { "Rotonda", "Tipo de carretera de la rotonda central. Una carretera de sentido único se coloca en el sentido de circulación de tu ciudad. Auto usa la red de las calles." },
            ["it-IT"] = new[] { "Rotatoria", "Tipo di strada della rotatoria centrale. Una strada a senso unico viene posata nel senso di marcia della tua città. Auto usa la rete delle vie." },
            ["pl-PL"] = new[] { "Rondo", "Typ drogi centralnego ronda. Droga jednokierunkowa jest układana zgodnie z kierunkiem ruchu w twoim mieście. Auto używa sieci ulic." },
            ["ru-RU"] = new[] { "Кольцо", "Тип дороги центрального кольца. Односторонняя дорога прокладывается по направлению движения вашего города. «Авто» использует сеть улиц." },
            ["ja-JP"] = new[] { "ラウンドアバウト", "中央のラウンドアバウトの道路タイプです。一方通行の道路は都市の通行方向に合わせて敷かれます。自動では通りのネットワークを使います。" },
            ["ko-KR"] = new[] { "회전교차로", "중앙 회전교차로의 도로 유형입니다. 일방통행 도로는 도시의 통행 방향에 맞춰 놓입니다. 자동은 거리 네트워크를 사용합니다." },
            ["zh-HANS"] = new[] { "环岛", "中央环岛的道路类型。单行道会按照城市的行车方向铺设。自动则使用街道网络。" },
            ["zh-HANT"] = new[] { "圓環", "中央圓環的道路類型。單行道會依照城市的行車方向鋪設。自動則使用街道網路。" },
            ["uk-UA"] = new[] { "Кільце", "Тип дороги центрального кільця. Одностороння дорога прокладається за напрямком руху вашого міста. «Авто» використовує мережу вулиць." },
            ["th-TH"] = new[] { "วงเวียน", "ประเภทถนนของวงเวียนกลาง ถนนเดินรถทางเดียวจะวางตามทิศทางการเดินรถของเมือง อัตโนมัติจะใช้เครือข่ายถนนในซอย" },
            ["vi-VN"] = new[] { "Vòng xuyến", "Loại đường của vòng xuyến trung tâm. Đường một chiều được đặt theo chiều lưu thông của thành phố. Tự động dùng mạng lưới đường phố." },
            ["nl-NL"] = new[] { "Rotonde", "Wegtype van de centrale rotonde. Een eenrichtingsweg wordt aangelegd in de rijrichting van je stad. Auto gebruikt het straatnetwerk." },
            ["ca-ES"] = new[] { "Rotonda", "Tipus de carretera de la rotonda central. Una carretera de sentit únic es col·loca en el sentit de circulació de la teva ciutat. Auto fa servir la xarxa dels carrers." },
            ["gl-ES"] = new[] { "Rotonda", "Tipo de estrada da rotonda central. Unha estrada de sentido único colócase no sentido de circulación da túa cidade. Auto usa a rede das rúas." },
            ["eu-ES"] = new[] { "Biribilgunea", "Erdiko biribilgunearen errepide mota. Norabide bakarreko errepidea zure hiriko zirkulazio-noranzkoan jartzen da. Autok kaleen sarea erabiltzen du." },
            ["oc-FR"] = new[] { "Revironda", "Tipe de rota de la revironda centrala. Una rota a sens unic es pausada dins lo sens de circulacion de vòstra vila. Auto utiliza lo malhum de las carrièras." },
            ["cy-GB"] = new[] { "Cylchfan", "Math o ffordd y gylchfan ganolog. Gosodir ffordd unffordd i gyfeiriad traffig eich dinas. Mae Awtomatig yn defnyddio rhwydwaith y strydoedd." },
            ["fy-NL"] = new[] { "Rotonde", "Dyktype fan de sintrale rotonde. In ienrjochtingsdyk wurdt yn de riidrjochting fan dyn stêd oanlein. Auto brûkt it strjittenetwurk." },
            ["br-FR"] = new[] { "Kroashent-tro", "Seurt hent ar c'hroashent-tro kreiz. Un hent unroud a vez lakaet e roud an tremenerezh en ho kêr. Emgefreek a implij rouedad ar straedoù." },
            ["sco-GB"] = new[] { "Roondaboot", "Road type o the central roondaboot. A wan-wey road is laid in the drivin direction o yer toun. Auto uises the street network." },
            ["sc-IT"] = new[] { "Rotatòria", "Tipu de istrada de sa rotatòria tzentrale. Un'istrada a sensu ùnicu benit posta in su sensu de tràficu de sa tzitade tua. Auto impreat sa rete de sas carreras." },
            ["cs-CZ"] = new[] { "Kruhový objezd", "Typ silnice centrálního kruhového objezdu. Jednosměrná silnice se položí ve směru jízdy vašeho města. Auto použije síť ulic." },
            ["da-DK"] = new[] { "Rundkørsel", "Vejtypen for den centrale rundkørsel. En ensrettet vej lægges i din bys kørselsretning. Auto bruger gadenettet." },
            ["nb-NO"] = new[] { "Rundkjøring", "Veitypen for den sentrale rundkjøringen. En enveiskjørt vei legges i byens kjøreretning. Auto bruker gatenettet." },
            ["sv-SE"] = new[] { "Rondell", "Vägtyp för den centrala rondellen. En enkelriktad väg läggs i stadens körriktning. Auto använder gatunätet." },
            ["fi-FI"] = new[] { "Liikenneympyrä", "Keskimmäisen liikenneympyrän tietyyppi. Yksisuuntainen tie asetetaan kaupunkisi ajosuuntaan. Automaattinen käyttää katuverkkoa." },
            ["hu-HU"] = new[] { "Körforgalom", "A központi körforgalom úttípusa. Az egyirányú út a város haladási irányában kerül lerakásra. Az Auto az utcák hálózatát használja." },
            ["hr-HR"] = new[] { "Kružni tok", "Vrsta ceste središnjeg kružnog toka. Jednosmjerna cesta postavlja se u smjeru vožnje tvog grada. Auto koristi mrežu ulica." },
            ["ro-RO"] = new[] { "Sens giratoriu", "Tipul de drum al sensului giratoriu central. Un drum cu sens unic este așezat în sensul de circulație al orașului tău. Auto folosește rețeaua de străzi." },
            ["el-GR"] = new[] { "Κυκλικός κόμβος", "Τύπος δρόμου του κεντρικού κυκλικού κόμβου. Ένας μονόδρομος τοποθετείται στην κατεύθυνση κυκλοφορίας της πόλης σας. Το Αυτόματο χρησιμοποιεί το δίκτυο των οδών." },
            ["tr-TR"] = new[] { "Göbek", "Merkezdeki göbeğin yol türü. Tek yönlü bir yol şehrinin trafik yönünde döşenir. Otomatik, sokak ağını kullanır." },
            ["id-ID"] = new[] { "Bundaran", "Jenis jalan bundaran pusat. Jalan satu arah dipasang mengikuti arah lalu lintas kotamu. Otomatis memakai jaringan jalan lingkungan." },
            ["fil-PH"] = new[] { "Rotonda", "Uri ng kalsada ng gitnang rotonda. Ang one-way na kalsada ay inilalatag ayon sa direksyon ng trapiko ng iyong lungsod. Ginagamit ng Auto ang network ng mga kalye." },
            ["hi-IN"] = new[] { "गोल चक्कर", "बीच के गोल चक्कर की सड़क का प्रकार। एकतरफ़ा सड़क आपके शहर की यातायात दिशा में बिछाई जाती है। ऑटो गलियों का नेटवर्क इस्तेमाल करता है।" },
            ["ar-SA"] = new[] { "دوّار", "نوع طريق الدوّار المركزي. يُوضع الطريق ذو الاتجاه الواحد في اتجاه السير في مدينتك. يستخدم الوضع التلقائي شبكة الشوارع." },
            ["fa-IR"] = new[] { "میدان", "نوع جاده میدان مرکزی. جاده یک‌طرفه در جهت رانندگی شهر شما گذاشته می‌شود. خودکار از شبکه خیابان‌ها استفاده می‌کند." },
            ["ab-GE"] = new[] { "Агьежь", "Ацентртәи агьежь амҩа атип. Хазы-хазы ицо амҩа ақалақь анеира-ааира ахырхарҭа ала иқәгылоуп. Автоматикала ауамҩақәа рыхьӡынҵа ахархәара амоуп." },
        };
    }
}
