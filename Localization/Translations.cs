using System.Collections.Generic;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;

namespace GridRoadGenerator.Localization
{
    /// <summary>
    /// Jeu de chaînes traduites pour une langue donnée.
    /// Le nom du mod ("Grid Road Generator") n'est jamais traduit.
    /// </summary>
    public class LocaleStrings
    {
        public string GroupMode;
        public string GroupGrid;

        public string ModeLabel;
        public string ModeDesc;

        public string EnumFitToArea;
        public string EnumFixedSpacing;

        public string ColumnsLabel;
        public string ColumnsDesc;

        public string RowsLabel;
        public string RowsDesc;

        public string SpacingLabel;
        public string SpacingDesc;
    }

    public static class Translations
    {
        public const string ModName = "Grid Road Generator";

        /// <summary>
        /// Construit le dictionnaire clé de localisation -> texte pour une langue,
        /// à partir des helpers de ModSetting (GetSettingsLocaleID, GetOptionLabelLocaleID, etc.).
        /// </summary>
        public static Dictionary<string, string> Build(GridRoadGeneratorSettings s, LocaleStrings t)
        {
            return new Dictionary<string, string>
            {
                { s.GetSettingsLocaleID(), ModName },

                { s.GetOptionGroupLocaleID(GridRoadGeneratorSettings.GroupMode), t.GroupMode },
                { s.GetOptionGroupLocaleID(GridRoadGeneratorSettings.GroupGrid), t.GroupGrid },

                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.Mode)), t.ModeLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.Mode)), t.ModeDesc },

                { s.GetEnumValueLocaleID(SpacingMode.FitToArea), t.EnumFitToArea },
                { s.GetEnumValueLocaleID(SpacingMode.FixedSpacing), t.EnumFixedSpacing },

                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.Columns)), t.ColumnsLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.Columns)), t.ColumnsDesc },

                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.Rows)), t.RowsLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.Rows)), t.RowsDesc },

                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.SpacingMeters)), t.SpacingLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.SpacingMeters)), t.SpacingDesc },
            };
        }

        /// <summary>
        /// Toutes les langues : les 12 officielles du jeu + 4 communautaires
        /// (pt-PT, th-TH, vi-VN, uk-UA). Les langues communautaires ne s'affichent
        /// que si un mod ajoutant ces locales est installé (ex. I18n EveryWhere).
        /// </summary>
        public static readonly Dictionary<string, LocaleStrings> All = new Dictionary<string, LocaleStrings>
        {
            // ============ LANGUES OFFICIELLES ============

            ["en-US"] = new LocaleStrings
            {
                GroupMode = "Mode",
                GroupGrid = "Grid",
                ModeLabel = "Spacing mode",
                ModeDesc = "How the internal roads are distributed within the selected rectangle.",
                EnumFitToArea = "Fit to area",
                EnumFixedSpacing = "Fixed spacing",
                ColumnsLabel = "Columns",
                ColumnsDesc = "Number of vertical roads generated inside the rectangle.",
                RowsLabel = "Rows",
                RowsDesc = "Number of horizontal roads generated inside the rectangle.",
                SpacingLabel = "Spacing (m)",
                SpacingDesc = "Distance in meters between roads (fixed spacing mode)."
            },

            ["fr-FR"] = new LocaleStrings
            {
                GroupMode = "Mode",
                GroupGrid = "Grille",
                ModeLabel = "Mode d'espacement",
                ModeDesc = "Comment les routes internes sont réparties dans le rectangle sélectionné.",
                EnumFitToArea = "Adapter à la zone",
                EnumFixedSpacing = "Espacement fixe",
                ColumnsLabel = "Colonnes",
                ColumnsDesc = "Nombre de routes verticales générées dans le rectangle.",
                RowsLabel = "Lignes",
                RowsDesc = "Nombre de routes horizontales générées dans le rectangle.",
                SpacingLabel = "Espacement (m)",
                SpacingDesc = "Distance en mètres entre les routes (mode espacement fixe)."
            },

            ["de-DE"] = new LocaleStrings
            {
                GroupMode = "Modus",
                GroupGrid = "Raster",
                ModeLabel = "Abstandsmodus",
                ModeDesc = "Wie die inneren Straßen im ausgewählten Rechteck verteilt werden.",
                EnumFitToArea = "An Fläche anpassen",
                EnumFixedSpacing = "Fester Abstand",
                ColumnsLabel = "Spalten",
                ColumnsDesc = "Anzahl der vertikalen Straßen im Rechteck.",
                RowsLabel = "Reihen",
                RowsDesc = "Anzahl der horizontalen Straßen im Rechteck.",
                SpacingLabel = "Abstand (m)",
                SpacingDesc = "Abstand in Metern zwischen den Straßen (Modus „fester Abstand\")."
            },

            ["es-ES"] = new LocaleStrings
            {
                GroupMode = "Modo",
                GroupGrid = "Cuadrícula",
                ModeLabel = "Modo de espaciado",
                ModeDesc = "Cómo se distribuyen las carreteras internas dentro del rectángulo seleccionado.",
                EnumFitToArea = "Ajustar al área",
                EnumFixedSpacing = "Espaciado fijo",
                ColumnsLabel = "Columnas",
                ColumnsDesc = "Número de carreteras verticales generadas dentro del rectángulo.",
                RowsLabel = "Filas",
                RowsDesc = "Número de carreteras horizontales generadas dentro del rectángulo.",
                SpacingLabel = "Espaciado (m)",
                SpacingDesc = "Distancia en metros entre carreteras (modo de espaciado fijo)."
            },

            ["it-IT"] = new LocaleStrings
            {
                GroupMode = "Modalità",
                GroupGrid = "Griglia",
                ModeLabel = "Modalità di spaziatura",
                ModeDesc = "Come vengono distribuite le strade interne nel rettangolo selezionato.",
                EnumFitToArea = "Adatta all'area",
                EnumFixedSpacing = "Spaziatura fissa",
                ColumnsLabel = "Colonne",
                ColumnsDesc = "Numero di strade verticali generate nel rettangolo.",
                RowsLabel = "Righe",
                RowsDesc = "Numero di strade orizzontali generate nel rettangolo.",
                SpacingLabel = "Spaziatura (m)",
                SpacingDesc = "Distanza in metri tra le strade (modalità spaziatura fissa)."
            },

            ["pl-PL"] = new LocaleStrings
            {
                GroupMode = "Tryb",
                GroupGrid = "Siatka",
                ModeLabel = "Tryb rozstawu",
                ModeDesc = "Sposób rozmieszczenia wewnętrznych dróg w zaznaczonym prostokącie.",
                EnumFitToArea = "Dopasuj do obszaru",
                EnumFixedSpacing = "Stały rozstaw",
                ColumnsLabel = "Kolumny",
                ColumnsDesc = "Liczba pionowych dróg generowanych w prostokącie.",
                RowsLabel = "Wiersze",
                RowsDesc = "Liczba poziomych dróg generowanych w prostokącie.",
                SpacingLabel = "Rozstaw (m)",
                SpacingDesc = "Odległość w metrach między drogami (tryb stałego rozstawu)."
            },

            ["pt-BR"] = new LocaleStrings
            {
                GroupMode = "Modo",
                GroupGrid = "Grade",
                ModeLabel = "Modo de espaçamento",
                ModeDesc = "Como as ruas internas são distribuídas dentro do retângulo selecionado.",
                EnumFitToArea = "Ajustar à área",
                EnumFixedSpacing = "Espaçamento fixo",
                ColumnsLabel = "Colunas",
                ColumnsDesc = "Número de ruas verticais geradas dentro do retângulo.",
                RowsLabel = "Linhas",
                RowsDesc = "Número de ruas horizontais geradas dentro do retângulo.",
                SpacingLabel = "Espaçamento (m)",
                SpacingDesc = "Distância em metros entre as ruas (modo de espaçamento fixo)."
            },

            ["ru-RU"] = new LocaleStrings
            {
                GroupMode = "Режим",
                GroupGrid = "Сетка",
                ModeLabel = "Режим интервала",
                ModeDesc = "Как внутренние дороги распределяются внутри выбранного прямоугольника.",
                EnumFitToArea = "Подогнать под область",
                EnumFixedSpacing = "Фиксированный интервал",
                ColumnsLabel = "Столбцы",
                ColumnsDesc = "Число вертикальных дорог внутри прямоугольника.",
                RowsLabel = "Ряды",
                RowsDesc = "Число горизонтальных дорог внутри прямоугольника.",
                SpacingLabel = "Интервал (м)",
                SpacingDesc = "Расстояние в метрах между дорогами (режим фиксированного интервала)."
            },

            ["ja-JP"] = new LocaleStrings
            {
                GroupMode = "モード",
                GroupGrid = "グリッド",
                ModeLabel = "間隔モード",
                ModeDesc = "選択した矩形内で内部道路をどのように配置するかを設定します。",
                EnumFitToArea = "エリアに合わせる",
                EnumFixedSpacing = "固定間隔",
                ColumnsLabel = "列数",
                ColumnsDesc = "矩形内に生成する縦方向の道路の数。",
                RowsLabel = "行数",
                RowsDesc = "矩形内に生成する横方向の道路の数。",
                SpacingLabel = "間隔 (m)",
                SpacingDesc = "道路間の距離（メートル、固定間隔モード）。"
            },

            ["ko-KR"] = new LocaleStrings
            {
                GroupMode = "모드",
                GroupGrid = "그리드",
                ModeLabel = "간격 모드",
                ModeDesc = "선택한 사각형 안에서 내부 도로를 배치하는 방식입니다.",
                EnumFitToArea = "영역에 맞추기",
                EnumFixedSpacing = "고정 간격",
                ColumnsLabel = "열",
                ColumnsDesc = "사각형 안에 생성할 세로 도로의 수.",
                RowsLabel = "행",
                RowsDesc = "사각형 안에 생성할 가로 도로의 수.",
                SpacingLabel = "간격 (m)",
                SpacingDesc = "도로 사이의 거리(미터, 고정 간격 모드)."
            },

            ["zh-HANS"] = new LocaleStrings
            {
                GroupMode = "模式",
                GroupGrid = "网格",
                ModeLabel = "间距模式",
                ModeDesc = "内部道路在所选矩形内的分布方式。",
                EnumFitToArea = "适应区域",
                EnumFixedSpacing = "固定间距",
                ColumnsLabel = "列数",
                ColumnsDesc = "矩形内生成的纵向道路数量。",
                RowsLabel = "行数",
                RowsDesc = "矩形内生成的横向道路数量。",
                SpacingLabel = "间距（米）",
                SpacingDesc = "道路之间的距离（米，固定间距模式）。"
            },

            ["zh-HANT"] = new LocaleStrings
            {
                GroupMode = "模式",
                GroupGrid = "網格",
                ModeLabel = "間距模式",
                ModeDesc = "內部道路在所選矩形內的分佈方式。",
                EnumFitToArea = "適應區域",
                EnumFixedSpacing = "固定間距",
                ColumnsLabel = "列數",
                ColumnsDesc = "矩形內生成的縱向道路數量。",
                RowsLabel = "行數",
                RowsDesc = "矩形內生成的橫向道路數量。",
                SpacingLabel = "間距（公尺）",
                SpacingDesc = "道路之間的距離（公尺，固定間距模式）。"
            },

            // ============ LANGUES COMMUNAUTAIRES ============

            // Portugais du Portugal — registre informel, adaptation contextuelle
            ["pt-PT"] = new LocaleStrings
            {
                GroupMode = "Modo",
                GroupGrid = "Grelha",
                ModeLabel = "Modo de espaçamento",
                ModeDesc = "Como as estradas internas são distribuídas dentro do retângulo que selecionaste.",
                EnumFitToArea = "Ajustar à área",
                EnumFixedSpacing = "Espaçamento fixo",
                ColumnsLabel = "Colunas",
                ColumnsDesc = "Número de estradas verticais geradas dentro do retângulo.",
                RowsLabel = "Linhas",
                RowsDesc = "Número de estradas horizontais geradas dentro do retângulo.",
                SpacingLabel = "Espaçamento (m)",
                SpacingDesc = "Distância em metros entre estradas (modo de espaçamento fixo)."
            },

            ["uk-UA"] = new LocaleStrings
            {
                GroupMode = "Режим",
                GroupGrid = "Сітка",
                ModeLabel = "Режим інтервалу",
                ModeDesc = "Як внутрішні дороги розподіляються всередині вибраного прямокутника.",
                EnumFitToArea = "Підігнати під область",
                EnumFixedSpacing = "Фіксований інтервал",
                ColumnsLabel = "Стовпці",
                ColumnsDesc = "Кількість вертикальних доріг усередині прямокутника.",
                RowsLabel = "Ряди",
                RowsDesc = "Кількість горизонтальних доріг усередині прямокутника.",
                SpacingLabel = "Інтервал (м)",
                SpacingDesc = "Відстань у метрах між дорогами (режим фіксованого інтервалу)."
            },

            ["th-TH"] = new LocaleStrings
            {
                GroupMode = "โหมด",
                GroupGrid = "ตาราง",
                ModeLabel = "โหมดระยะห่าง",
                ModeDesc = "วิธีกระจายถนนภายในสี่เหลี่ยมที่เลือก",
                EnumFitToArea = "พอดีกับพื้นที่",
                EnumFixedSpacing = "ระยะห่างคงที่",
                ColumnsLabel = "คอลัมน์",
                ColumnsDesc = "จำนวนถนนแนวตั้งที่สร้างภายในสี่เหลี่ยม",
                RowsLabel = "แถว",
                RowsDesc = "จำนวนถนนแนวนอนที่สร้างภายในสี่เหลี่ยม",
                SpacingLabel = "ระยะห่าง (ม.)",
                SpacingDesc = "ระยะทางเป็นเมตรระหว่างถนน (โหมดระยะห่างคงที่)"
            },

            ["vi-VN"] = new LocaleStrings
            {
                GroupMode = "Chế độ",
                GroupGrid = "Lưới",
                ModeLabel = "Chế độ khoảng cách",
                ModeDesc = "Cách phân bố các con đường bên trong hình chữ nhật đã chọn.",
                EnumFitToArea = "Vừa với khu vực",
                EnumFixedSpacing = "Khoảng cách cố định",
                ColumnsLabel = "Cột",
                ColumnsDesc = "Số đường dọc được tạo bên trong hình chữ nhật.",
                RowsLabel = "Hàng",
                RowsDesc = "Số đường ngang được tạo bên trong hình chữ nhật.",
                SpacingLabel = "Khoảng cách (m)",
                SpacingDesc = "Khoảng cách tính bằng mét giữa các con đường (chế độ khoảng cách cố định)."
            },
        };
    }
}
