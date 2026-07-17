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

        public string GroupKeybindings;

        public string ToggleToolLabel;
        public string ToggleToolDesc;

        public string ConfirmGridLabel;
        public string ConfirmGridDesc;

        // Panneau UI in-game
        public string UINodesSelected;
        public string UIGenerate;
        public string UIClearAll;
        /// <summary>Libellé court de l'espacement (sans unité : le "m" est affiché dans le champ).</summary>
        public string UISpacingShort;
        /// <summary>Libellé de la rangée affichant le prefab de route utilisé.</summary>
        public string UIRoadPrefab;

        // Tooltips contextuels près du curseur
        public string TooltipSelectNode;
        public string TooltipRemoveLast;
        public string TooltipConfirm;
        public string TooltipInvalidPerimeter;
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

                { s.GetOptionGroupLocaleID(GridRoadGeneratorSettings.GroupKeybindings), t.GroupKeybindings },

                // Lignes du menu Options (une par propriété ProxyBinding).
                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.ToggleToolBinding)), t.ToggleToolLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.ToggleToolBinding)), t.ToggleToolDesc },
                { s.GetOptionLabelLocaleID(nameof(GridRoadGeneratorSettings.ConfirmGridBinding)), t.ConfirmGridLabel },
                { s.GetOptionDescLocaleID(nameof(GridRoadGeneratorSettings.ConfirmGridBinding)), t.ConfirmGridDesc },

                // Libellés des actions dans l'écran de réassignation des touches du jeu.
                { s.GetBindingKeyLocaleID(GridRoadGeneratorSettings.ActionToggleTool), t.ToggleToolLabel },
                { s.GetBindingKeyLocaleID(GridRoadGeneratorSettings.ActionConfirmGrid), t.ConfirmGridLabel },
                { s.GetBindingKeyHintLocaleID(GridRoadGeneratorSettings.ActionToggleTool), t.ToggleToolLabel },
                { s.GetBindingKeyHintLocaleID(GridRoadGeneratorSettings.ActionConfirmGrid), t.ConfirmGridLabel },

                // Nom de la map d'input du mod (jamais traduit : nom du mod).
                { s.GetBindingMapLocaleID(), ModName },

                // Clés plates du panneau UI in-game (lues par translate() côté React).
                // Les libellés mode/colonnes/lignes/espacement réutilisent ceux des Options.
                { "GridRoadGenerator.UI.Title", ModName },
                { "GridRoadGenerator.UI.Mode", t.ModeLabel },
                { "GridRoadGenerator.UI.ModeFit", t.EnumFitToArea },
                { "GridRoadGenerator.UI.ModeFixed", t.EnumFixedSpacing },
                { "GridRoadGenerator.UI.Columns", t.ColumnsLabel },
                { "GridRoadGenerator.UI.Rows", t.RowsLabel },
                { "GridRoadGenerator.UI.Spacing", t.SpacingLabel },
                { "GridRoadGenerator.UI.SpacingShort", t.UISpacingShort },
                { "GridRoadGenerator.UI.RoadPrefab", t.UIRoadPrefab },
                { "GridRoadGenerator.UI.NodesSelected", t.UINodesSelected },
                { "GridRoadGenerator.UI.Generate", t.UIGenerate },
                { "GridRoadGenerator.UI.ClearAll", t.UIClearAll },

                // Tooltips contextuels près du curseur.
                { "GridRoadGenerator.Tooltip.SelectNode", t.TooltipSelectNode },
                { "GridRoadGenerator.Tooltip.RemoveLast", t.TooltipRemoveLast },
                { "GridRoadGenerator.Tooltip.Confirm", t.TooltipConfirm },
                { "GridRoadGenerator.Tooltip.InvalidPerimeter", t.TooltipInvalidPerimeter },
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
                SpacingDesc = "Distance in meters between roads (fixed spacing mode).",
                GroupKeybindings = "Key bindings",
                ToggleToolLabel = "Toggle grid tool",
                ToggleToolDesc = "Activates or deactivates the grid road tool. Then click road nodes to outline the perimeter.",
                ConfirmGridLabel = "Confirm grid",
                ConfirmGridDesc = "Builds the previewed grid inside the selected perimeter.",
                UINodesSelected = "Selected nodes",
                UIGenerate = "Generate grid",
                UIClearAll = "Clear all",
                UISpacingShort = "Spacing",
                UIRoadPrefab = "Road",
                TooltipSelectNode = "Select a road node to outline the perimeter",
                TooltipRemoveLast = "Right-click to remove the last node",
                TooltipConfirm = "Press Enter or click Generate to build the grid",
                TooltipInvalidPerimeter = "Invalid perimeter (area too small or nodes aligned)"
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
                SpacingDesc = "Distance en mètres entre les routes (mode espacement fixe).",
                GroupKeybindings = "Raccourcis clavier",
                ToggleToolLabel = "Activer l'outil de grille",
                ToggleToolDesc = "Active ou désactive l'outil de grille. Clique ensuite sur des nœuds de route pour tracer le périmètre.",
                ConfirmGridLabel = "Valider la grille",
                ConfirmGridDesc = "Construit la grille prévisualisée à l'intérieur du périmètre sélectionné.",
                UINodesSelected = "Nœuds sélectionnés",
                UIGenerate = "Générer la grille",
                UIClearAll = "Tout annuler",
                UISpacingShort = "Espacement",
                UIRoadPrefab = "Route",
                TooltipSelectNode = "Sélectionne un nœud de route pour tracer le périmètre",
                TooltipRemoveLast = "Clic droit pour retirer le dernier nœud",
                TooltipConfirm = "Appuie sur Entrée ou clique sur Générer pour construire la grille",
                TooltipInvalidPerimeter = "Périmètre invalide (aire trop petite ou nœuds alignés)"
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
                SpacingDesc = "Abstand in Metern zwischen den Straßen (Modus „fester Abstand\").",
                GroupKeybindings = "Tastenbelegung",
                ToggleToolLabel = "Rasterwerkzeug umschalten",
                ToggleToolDesc = "Aktiviert oder deaktiviert das Rasterwerkzeug. Klicke dann auf Straßenknoten, um den Umriss festzulegen.",
                ConfirmGridLabel = "Raster bestätigen",
                ConfirmGridDesc = "Baut das in der Vorschau angezeigte Raster innerhalb des gewählten Umrisses.",
                UINodesSelected = "Ausgewählte Knoten",
                UIGenerate = "Raster erzeugen",
                UIClearAll = "Alles abbrechen",
                UISpacingShort = "Abstand",
                UIRoadPrefab = "Straße",
                TooltipSelectNode = "Wähle einen Straßenknoten, um den Umriss festzulegen",
                TooltipRemoveLast = "Rechtsklick entfernt den letzten Knoten",
                TooltipConfirm = "Drücke Eingabe oder klicke auf Erzeugen, um das Raster zu bauen",
                TooltipInvalidPerimeter = "Ungültiger Umriss (Fläche zu klein oder Knoten auf einer Linie)"
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
                SpacingDesc = "Distancia en metros entre carreteras (modo de espaciado fijo).",
                GroupKeybindings = "Atajos de teclado",
                ToggleToolLabel = "Alternar herramienta de cuadrícula",
                ToggleToolDesc = "Activa o desactiva la herramienta de cuadrícula. Luego haz clic en nodos de carretera para delimitar el perímetro.",
                ConfirmGridLabel = "Confirmar cuadrícula",
                ConfirmGridDesc = "Construye la cuadrícula previsualizada dentro del perímetro seleccionado.",
                UINodesSelected = "Nodos seleccionados",
                UIGenerate = "Generar cuadrícula",
                UIClearAll = "Cancelar todo",
                UISpacingShort = "Espaciado",
                UIRoadPrefab = "Carretera",
                TooltipSelectNode = "Selecciona un nodo de carretera para delimitar el perímetro",
                TooltipRemoveLast = "Clic derecho para quitar el último nodo",
                TooltipConfirm = "Pulsa Intro o haz clic en Generar para construir la cuadrícula",
                TooltipInvalidPerimeter = "Perímetro no válido (área demasiado pequeña o nodos alineados)"
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
                SpacingDesc = "Distanza in metri tra le strade (modalità spaziatura fissa).",
                GroupKeybindings = "Scorciatoie da tastiera",
                ToggleToolLabel = "Attiva/disattiva strumento griglia",
                ToggleToolDesc = "Attiva o disattiva lo strumento griglia. Poi clicca sui nodi stradali per delimitare il perimetro.",
                ConfirmGridLabel = "Conferma griglia",
                ConfirmGridDesc = "Costruisce la griglia in anteprima all'interno del perimetro selezionato.",
                UINodesSelected = "Nodi selezionati",
                UIGenerate = "Genera griglia",
                UIClearAll = "Annulla tutto",
                UISpacingShort = "Spaziatura",
                UIRoadPrefab = "Strada",
                TooltipSelectNode = "Seleziona un nodo stradale per delimitare il perimetro",
                TooltipRemoveLast = "Clic destro per rimuovere l'ultimo nodo",
                TooltipConfirm = "Premi Invio o clicca su Genera per costruire la griglia",
                TooltipInvalidPerimeter = "Perimetro non valido (area troppo piccola o nodi allineati)"
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
                SpacingDesc = "Odległość w metrach między drogami (tryb stałego rozstawu).",
                GroupKeybindings = "Skróty klawiszowe",
                ToggleToolLabel = "Przełącz narzędzie siatki",
                ToggleToolDesc = "Włącza lub wyłącza narzędzie siatki dróg. Następnie klikaj węzły dróg, aby wyznaczyć obwód.",
                ConfirmGridLabel = "Zatwierdź siatkę",
                ConfirmGridDesc = "Buduje podglądaną siatkę wewnątrz wybranego obwodu.",
                UINodesSelected = "Zaznaczone węzły",
                UIGenerate = "Generuj siatkę",
                UIClearAll = "Anuluj wszystko",
                UISpacingShort = "Rozstaw",
                UIRoadPrefab = "Droga",
                TooltipSelectNode = "Zaznacz węzeł drogi, aby wyznaczyć obwód",
                TooltipRemoveLast = "Kliknij prawym przyciskiem, aby usunąć ostatni węzeł",
                TooltipConfirm = "Naciśnij Enter lub kliknij Generuj, aby zbudować siatkę",
                TooltipInvalidPerimeter = "Nieprawidłowy obwód (zbyt mała powierzchnia lub węzły w jednej linii)"
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
                SpacingDesc = "Distância em metros entre as ruas (modo de espaçamento fixo).",
                GroupKeybindings = "Atalhos de teclado",
                ToggleToolLabel = "Alternar ferramenta de grade",
                ToggleToolDesc = "Ativa ou desativa a ferramenta de grade. Depois clique nos nós de rua para delimitar o perímetro.",
                ConfirmGridLabel = "Confirmar grade",
                ConfirmGridDesc = "Constrói a grade pré-visualizada dentro do perímetro selecionado.",
                UINodesSelected = "Nós selecionados",
                UIGenerate = "Gerar grade",
                UIClearAll = "Cancelar tudo",
                UISpacingShort = "Espaçamento",
                UIRoadPrefab = "Rua",
                TooltipSelectNode = "Selecione um nó de rua para delimitar o perímetro",
                TooltipRemoveLast = "Clique com o botão direito para remover o último nó",
                TooltipConfirm = "Pressione Enter ou clique em Gerar para construir a grade",
                TooltipInvalidPerimeter = "Perímetro inválido (área muito pequena ou nós alinhados)"
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
                SpacingDesc = "Расстояние в метрах между дорогами (режим фиксированного интервала).",
                GroupKeybindings = "Горячие клавиши",
                ToggleToolLabel = "Переключить инструмент сетки",
                ToggleToolDesc = "Включает или выключает инструмент сетки дорог. Затем щёлкайте по узлам дорог, чтобы очертить периметр.",
                ConfirmGridLabel = "Подтвердить сетку",
                ConfirmGridDesc = "Строит показанную в предпросмотре сетку внутри выбранного периметра.",
                UINodesSelected = "Выбрано узлов",
                UIGenerate = "Создать сетку",
                UIClearAll = "Отменить всё",
                UISpacingShort = "Интервал",
                UIRoadPrefab = "Дорога",
                TooltipSelectNode = "Выберите узел дороги, чтобы очертить периметр",
                TooltipRemoveLast = "Правый клик — убрать последний узел",
                TooltipConfirm = "Нажмите Enter или кнопку «Создать», чтобы построить сетку",
                TooltipInvalidPerimeter = "Недопустимый периметр (слишком малая площадь или узлы на одной линии)"
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
                SpacingDesc = "道路間の距離（メートル、固定間隔モード）。",
                GroupKeybindings = "キー割り当て",
                ToggleToolLabel = "グリッドツールの切り替え",
                ToggleToolDesc = "グリッド道路ツールを有効/無効にします。その後、道路のノードをクリックして外周を指定します。",
                ConfirmGridLabel = "グリッドを確定",
                ConfirmGridDesc = "選択した外周の内側に、プレビュー中のグリッドを建設します。",
                UINodesSelected = "選択中のノード",
                UIGenerate = "グリッドを生成",
                UIClearAll = "すべて取り消す",
                UISpacingShort = "間隔",
                UIRoadPrefab = "道路",
                TooltipSelectNode = "道路のノードをクリックして外周を指定します",
                TooltipRemoveLast = "右クリックで最後のノードを削除します",
                TooltipConfirm = "Enter キーまたは「生成」でグリッドを建設します",
                TooltipInvalidPerimeter = "無効な外周です（面積が小さすぎるか、ノードが一直線上にあります）"
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
                SpacingDesc = "도로 사이의 거리(미터, 고정 간격 모드).",
                GroupKeybindings = "단축키",
                ToggleToolLabel = "그리드 도구 전환",
                ToggleToolDesc = "그리드 도로 도구를 켜거나 끕니다. 그런 다음 도로 노드를 클릭해 경계를 지정하세요.",
                ConfirmGridLabel = "그리드 확정",
                ConfirmGridDesc = "선택한 경계 안에 미리 보기 중인 그리드를 건설합니다.",
                UINodesSelected = "선택한 노드",
                UIGenerate = "그리드 생성",
                UIClearAll = "모두 취소",
                UISpacingShort = "간격",
                UIRoadPrefab = "도로",
                TooltipSelectNode = "도로 노드를 클릭해 경계를 지정하세요",
                TooltipRemoveLast = "우클릭으로 마지막 노드를 제거합니다",
                TooltipConfirm = "Enter 키 또는 생성 버튼으로 그리드를 건설합니다",
                TooltipInvalidPerimeter = "잘못된 경계입니다(면적이 너무 작거나 노드가 일직선에 있음)"
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
                SpacingDesc = "道路之间的距离（米，固定间距模式）。",
                GroupKeybindings = "快捷键",
                ToggleToolLabel = "切换网格工具",
                ToggleToolDesc = "启用或停用网格道路工具。然后点击道路节点以圈定周界。",
                ConfirmGridLabel = "确认网格",
                ConfirmGridDesc = "在所选周界内建造预览中的网格。",
                UINodesSelected = "已选节点",
                UIGenerate = "生成网格",
                UIClearAll = "全部取消",
                UISpacingShort = "间距",
                UIRoadPrefab = "道路",
                TooltipSelectNode = "点击道路节点以圈定周界",
                TooltipRemoveLast = "右键点击移除最后一个节点",
                TooltipConfirm = "按回车键或点击“生成”建造网格",
                TooltipInvalidPerimeter = "周界无效（面积过小或节点共线）"
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
                SpacingDesc = "道路之間的距離（公尺，固定間距模式）。",
                GroupKeybindings = "快捷鍵",
                ToggleToolLabel = "切換網格工具",
                ToggleToolDesc = "啟用或停用網格道路工具。然後點擊道路節點以圈定周界。",
                ConfirmGridLabel = "確認網格",
                ConfirmGridDesc = "在所選周界內建造預覽中的網格。",
                UINodesSelected = "已選節點",
                UIGenerate = "生成網格",
                UIClearAll = "全部取消",
                UISpacingShort = "間距",
                UIRoadPrefab = "道路",
                TooltipSelectNode = "點擊道路節點以圈定周界",
                TooltipRemoveLast = "按右鍵移除最後一個節點",
                TooltipConfirm = "按 Enter 鍵或點擊「生成」建造網格",
                TooltipInvalidPerimeter = "周界無效（面積過小或節點共線）"
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
                SpacingDesc = "Distância em metros entre estradas (modo de espaçamento fixo).",
                GroupKeybindings = "Atalhos de teclado",
                ToggleToolLabel = "Ativar/desativar ferramenta de grelha",
                ToggleToolDesc = "Liga ou desliga a ferramenta de grelha. Depois clica nos nós de estrada para delimitares o perímetro.",
                ConfirmGridLabel = "Confirmar grelha",
                ConfirmGridDesc = "Constrói a grelha pré-visualizada dentro do perímetro que selecionaste.",
                UINodesSelected = "Nós selecionados",
                UIGenerate = "Gerar grelha",
                UIClearAll = "Cancelar tudo",
                UISpacingShort = "Espaçamento",
                UIRoadPrefab = "Estrada",
                TooltipSelectNode = "Clica num nó de estrada para delimitares o perímetro",
                TooltipRemoveLast = "Clica com o botão direito para retirares o último nó",
                TooltipConfirm = "Carrega em Enter ou clica em gerar para construíres a grelha",
                TooltipInvalidPerimeter = "Perímetro inválido (área demasiado pequena ou nós alinhados)"
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
                SpacingDesc = "Відстань у метрах між дорогами (режим фіксованого інтервалу).",
                GroupKeybindings = "Гарячі клавіші",
                ToggleToolLabel = "Перемкнути інструмент сітки",
                ToggleToolDesc = "Вмикає або вимикає інструмент сітки доріг. Далі клацай по вузлах доріг, щоб окреслити периметр.",
                ConfirmGridLabel = "Підтвердити сітку",
                ConfirmGridDesc = "Будує сітку з попереднього перегляду всередині вибраного периметра.",
                UINodesSelected = "Вибрано вузлів",
                UIGenerate = "Створити сітку",
                UIClearAll = "Скасувати все",
                UISpacingShort = "Інтервал",
                UIRoadPrefab = "Дорога",
                TooltipSelectNode = "Клацни вузол дороги, щоб окреслити периметр",
                TooltipRemoveLast = "Правий клік — прибрати останній вузол",
                TooltipConfirm = "Натисни Enter або кнопку «Створити», щоб побудувати сітку",
                TooltipInvalidPerimeter = "Недійсний периметр (замала площа або вузли на одній лінії)"
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
                SpacingDesc = "ระยะทางเป็นเมตรระหว่างถนน (โหมดระยะห่างคงที่)",
                GroupKeybindings = "ปุ่มลัด",
                ToggleToolLabel = "สลับเครื่องมือตาราง",
                ToggleToolDesc = "เปิดหรือปิดเครื่องมือถนนแบบตาราง จากนั้นคลิกที่จุดเชื่อมถนนเพื่อกำหนดขอบเขต",
                ConfirmGridLabel = "ยืนยันตาราง",
                ConfirmGridDesc = "สร้างตารางที่แสดงตัวอย่างไว้ภายในขอบเขตที่เลือก",
                UINodesSelected = "จุดเชื่อมที่เลือก",
                UIGenerate = "สร้างตาราง",
                UIClearAll = "ยกเลิกทั้งหมด",
                UISpacingShort = "ระยะห่าง",
                UIRoadPrefab = "ถนน",
                TooltipSelectNode = "คลิกจุดเชื่อมถนนเพื่อกำหนดขอบเขต",
                TooltipRemoveLast = "คลิกขวาเพื่อลบจุดเชื่อมล่าสุด",
                TooltipConfirm = "กด Enter หรือคลิกสร้างเพื่อก่อสร้างตาราง",
                TooltipInvalidPerimeter = "ขอบเขตไม่ถูกต้อง (พื้นที่เล็กเกินไปหรือจุดเชื่อมอยู่ในแนวเดียวกัน)"
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
                SpacingDesc = "Khoảng cách tính bằng mét giữa các con đường (chế độ khoảng cách cố định).",
                GroupKeybindings = "Phím tắt",
                ToggleToolLabel = "Bật/tắt công cụ lưới",
                ToggleToolDesc = "Bật hoặc tắt công cụ đường lưới. Sau đó nhấp vào các nút giao đường để khoanh vùng chu vi.",
                ConfirmGridLabel = "Xác nhận lưới",
                ConfirmGridDesc = "Xây dựng lưới đang xem trước bên trong chu vi đã chọn.",
                UINodesSelected = "Nút đã chọn",
                UIGenerate = "Tạo lưới",
                UIClearAll = "Hủy tất cả",
                UISpacingShort = "Khoảng cách",
                UIRoadPrefab = "Đường",
                TooltipSelectNode = "Nhấp vào nút giao đường để khoanh vùng chu vi",
                TooltipRemoveLast = "Nhấp chuột phải để bỏ nút cuối cùng",
                TooltipConfirm = "Nhấn Enter hoặc bấm Tạo lưới để xây dựng",
                TooltipInvalidPerimeter = "Chu vi không hợp lệ (diện tích quá nhỏ hoặc các nút thẳng hàng)"
            },
        };
    }
}
