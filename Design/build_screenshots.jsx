// Gera Screenshots.psd (1920 x 1080) no estilo da Thumbnail: um grupo por modo, texto editável
// (TAN Headline + Montserrat), cartões, pílulas e fundos dos ícones como formas vetoriais com
// estilos de camada, ícones como objetos inteligentes (SVG da pasta icons).
// Photoshop: Ficheiro > Scripts > Procurar... e escolher este ficheiro.
#target photoshop
app.displayDialogs = DialogModes.NO;
app.preferences.rulerUnits = Units.PIXELS;
app.preferences.typeUnits = TypeUnits.PIXELS;

var ROOT = new File($.fileName).parent.fsName;
var W = 1920, H = 1080;
var BLUE = [14, 66, 178], VIOLET = [200, 108, 228], CYAN = [94, 226, 232], DEEP = [10, 76, 176], WHITE = [255, 255, 255];

var PATTERNS = [
    ['Grid', 'patternGrid', 'Classic street grid: columns, rows or fixed spacing, angle, cul-de-sacs with turning circles and an avenue with a roundabout.'],
    ['Tree', 'patternTree', 'A collector along the area with branch streets and pairs of cul-de-sacs. All through traffic stays on the collector.'],
    ['Organic', 'patternOrganic', 'A gently winding avenue with curved streets and cul-de-sacs, like a real suburb.'],
    ['Terrain', 'patternContour', 'Streets along the contour lines, nearly level, linked by uphill streets never steeper than 15%.'],
    ['Mixed', 'patternMixed', 'A radial centre surrounded by organic streets, joined by a ring road.'],
    ['Loop', 'patternLoop', 'Collector roads divide the area into blocks, each served by an internal loop street.'],
    ['Superblock', 'patternSuperblock', 'Barcelona-style zones separated by pedestrian streets. Through traffic stays on the collectors.'],
    ['Concentric', 'patternConcentric', 'Rings that copy the shape of the perimeter, linked to the outside by radial connections.'],
    ['Radial', 'patternRadial', 'A central roundabout with equally spaced avenues and circular layers.']
];
var AREAS = [
    ['Existing perimeter', 'selectionExisting', 'Click the nodes of roads that already surround the area. Double-click a node to select the whole block.'],
    ['Draw area', 'selectionDraw', 'Click points on the terrain, like the district tool, then drag any point to reshape the area.'],
    ['Paint area', 'selectionPaint', 'Paint the area with a round or square brush from 1 to 1000 m. Right-click erases.']
];

function cID(s) { return charIDToTypeID(s); }
function sID(s) { return stringIDToTypeID(s); }

// ---------------------------------------------------------------- fontes
function findFont(family, style) {
    for (var i = 0; i < app.fonts.length; i++) {
        var f = app.fonts[i];
        if (f.family.toLowerCase() == family.toLowerCase() && (!style || f.style.toLowerCase() == style.toLowerCase())
            && f.postScriptName.indexOf('Greek') < 0) return f.postScriptName;
    }
    for (var j = 0; j < app.fonts.length; j++) {
        if (app.fonts[j].family.toLowerCase().indexOf(family.toLowerCase()) >= 0 && app.fonts[j].postScriptName.indexOf('Greek') < 0) return app.fonts[j].postScriptName;
    }
    return 'ArialMT';
}
var FONT_TITLE = findFont('TAN - HEADLINE', null);
var FONT_BOLD = findFont('Montserrat-Arabic', 'ExtraBold');
var FONT_SEMI = findFont('Montserrat-Arabic', 'SemiBold');
var FONT_MED = findFont('Montserrat-Arabic', 'Medium');

// ---------------------------------------------------------------- utilitários
function rgb(c) {
    var d = new ActionDescriptor();
    d.putDouble(cID('Rd  '), c[0]); d.putDouble(cID('Grn '), c[1]); d.putDouble(cID('Bl  '), c[2]);
    return d;
}
function solidColor(c) { var s = new SolidColor(); s.rgb.red = c[0]; s.rgb.green = c[1]; s.rgb.blue = c[2]; return s; }

function gradientFill(colors, angle, opacities) {
    var g = new ActionDescriptor();
    g.putString(cID('Nm  '), 'Custom');
    g.putEnumerated(cID('GrdF'), cID('GrdF'), cID('CstS'));
    g.putDouble(cID('Intr'), 4096);
    var cl = new ActionList();
    for (var i = 0; i < colors.length; i++) {
        var s = new ActionDescriptor();
        s.putObject(cID('Clr '), cID('RGBC'), rgb(colors[i][0]));
        s.putEnumerated(cID('Type'), cID('Clry'), cID('UsrS'));
        s.putInteger(cID('Lctn'), colors[i][1]);
        s.putInteger(cID('Mdpn'), 50);
        cl.putObject(cID('Clrt'), s);
    }
    g.putList(cID('Clrs'), cl);
    var tl = new ActionList();
    var ops = opacities || [[100, 0], [100, 4096]];
    for (var k = 0; k < ops.length; k++) {
        var t = new ActionDescriptor();
        t.putUnitDouble(cID('Opct'), cID('#Prc'), ops[k][0]);
        t.putInteger(cID('Lctn'), ops[k][1]);
        t.putInteger(cID('Mdpn'), 50);
        tl.putObject(cID('TrnS'), t);
    }
    g.putList(cID('Trns'), tl);
    var f = new ActionDescriptor();
    f.putUnitDouble(cID('Angl'), cID('#Ang'), angle);
    f.putEnumerated(cID('Type'), cID('GrdT'), cID('Lnr '));
    f.putBoolean(cID('Dthr'), true);
    f.putObject(cID('Grad'), cID('Grdn'), g);
    return f;
}

// Forma retangular (cantos arredondados) com preenchimento sólido ou em gradiente.
function shape(group, name, l, t, r, b, radius, fill) {
    var desc = new ActionDescriptor();
    var ref = new ActionReference(); ref.putClass(sID('contentLayer'));
    desc.putReference(cID('null'), ref);
    var ld = new ActionDescriptor();
    if (fill.gradient) ld.putObject(cID('Type'), sID('gradientLayer'), gradientFill(fill.gradient, fill.angle, fill.opacities));
    else { var sc = new ActionDescriptor(); sc.putObject(cID('Clr '), cID('RGBC'), rgb(fill.color)); ld.putObject(cID('Type'), sID('solidColorLayer'), sc); }
    var sh = new ActionDescriptor();
    sh.putInteger(sID('unitValueQuadVersion'), 1);
    sh.putUnitDouble(cID('Top '), cID('#Pxl'), t); sh.putUnitDouble(cID('Left'), cID('#Pxl'), l);
    sh.putUnitDouble(cID('Btom'), cID('#Pxl'), b); sh.putUnitDouble(cID('Rght'), cID('#Pxl'), r);
    if (radius > 0) {
        sh.putUnitDouble(sID('topRight'), cID('#Pxl'), radius); sh.putUnitDouble(sID('topLeft'), cID('#Pxl'), radius);
        sh.putUnitDouble(sID('bottomLeft'), cID('#Pxl'), radius); sh.putUnitDouble(sID('bottomRight'), cID('#Pxl'), radius);
    }
    ld.putObject(cID('Shp '), cID('Rctn'), sh);
    desc.putObject(cID('Usng'), sID('contentLayer'), ld);
    executeAction(cID('Mk  '), desc, DialogModes.NO);
    return into(group, name);
}

function effects(opts) {
    var d = new ActionDescriptor();
    var ref = new ActionReference(); ref.putProperty(cID('Prpr'), cID('Lefx')); ref.putEnumerated(cID('Lyr '), cID('Ordn'), cID('Trgt'));
    d.putReference(cID('null'), ref);
    var fx = new ActionDescriptor();
    fx.putUnitDouble(cID('Scl '), cID('#Prc'), 100);
    if (opts.stroke) {
        var st = new ActionDescriptor();
        st.putBoolean(cID('enab'), true);
        st.putEnumerated(cID('Styl'), cID('FStl'), cID('InsF'));
        st.putEnumerated(cID('PntT'), cID('FrFl'), cID('SClr'));
        st.putEnumerated(cID('Md  '), cID('BlnM'), cID('Nrml'));
        st.putUnitDouble(cID('Opct'), cID('#Prc'), 100);
        st.putUnitDouble(cID('Sz  '), cID('#Pxl'), opts.stroke);
        st.putObject(cID('Clr '), cID('RGBC'), rgb(WHITE));
        fx.putObject(cID('FrFX'), cID('FrFX'), st);
    }
    if (opts.shadow) {
        var ds = new ActionDescriptor();
        ds.putBoolean(cID('enab'), true);
        ds.putEnumerated(cID('Md  '), cID('BlnM'), cID('Mltp'));
        ds.putObject(cID('Clr '), cID('RGBC'), rgb([30, 10, 70]));
        ds.putUnitDouble(cID('Opct'), cID('#Prc'), opts.shadow[0]);
        ds.putBoolean(cID('uglg'), false);
        ds.putUnitDouble(cID('lagl'), cID('#Ang'), 90);
        ds.putUnitDouble(cID('Dstn'), cID('#Pxl'), opts.shadow[1]);
        ds.putUnitDouble(cID('Ckmt'), cID('#Pxl'), 0);
        ds.putUnitDouble(cID('blur'), cID('#Pxl'), opts.shadow[2]);
        fx.putObject(cID('DrSh'), cID('DrSh'), ds);
    }
    d.putObject(cID('T   '), cID('Lefx'), fx);
    executeAction(cID('setd'), d, DialogModes.NO);
}

// Põe a camada ativa no topo do grupo e dá-lhe o nome.
function into(group, name) {
    var layer = app.activeDocument.activeLayer;
    layer.name = name;
    if (group && layer.parent != group) layer.move(group, ElementPlacement.PLACEATBEGINNING);
    return layer;
}

function text(group, name, contents, font, size, color, x, y, opts) {
    opts = opts || {};
    var layer = group.artLayers.add();
    layer.kind = LayerKind.TEXT;
    layer.name = name;
    var t = layer.textItem;
    if (opts.box) {
        t.kind = TextType.PARAGRAPHTEXT;
        t.position = [x, y];
        t.width = opts.box[0];
        t.height = opts.box[1];
    } else {
        t.position = [x, y];
    }
    t.contents = contents;
    t.font = font;
    t.size = size;
    t.color = solidColor(color);
    if (opts.tracking) t.tracking = opts.tracking;
    if (opts.leading) { t.useAutoLeading = false; t.leading = opts.leading; }
    if (opts.center) t.justification = Justification.CENTER;
    layer.move(group, ElementPlacement.PLACEATBEGINNING);
    return layer;
}

function place(group, name, path, x, y, size, opacity) {
    var d = new ActionDescriptor();
    d.putPath(cID('null'), new File(path));
    d.putEnumerated(cID('FTcs'), cID('QCSt'), cID('Qcsa'));
    executeAction(cID('Plc '), d, DialogModes.NO);
    var layer = into(group, name);
    var b = layer.bounds;
    var w = b[2].as('px') - b[0].as('px'), h = b[3].as('px') - b[1].as('px');
    if (size) {
        var s = size / Math.max(w, h) * 100;
        layer.resize(s, s, AnchorPosition.TOPLEFT);
    }
    b = layer.bounds;
    layer.translate(x - b[0].as('px'), y - b[1].as('px'));
    if (opacity !== undefined) layer.opacity = opacity;
    return layer;
}

function iconFile(name) { return ROOT + '/icons/' + name + '.svg'; }

// Fundo do ícone (forma editável) + ícone por cima.
function iconTile(group, name, iconName, x, y, size) {
    shape(group, 'Fundo do ícone ' + name, x, y, x + size, y + size, Math.round(size / 5), { color: WHITE });
    app.activeDocument.activeLayer.fillOpacity = 16;
    effects({ stroke: Math.max(4, Math.round(size / 32)), shadow: [35, 6, 14] });
    var s = Math.round(size * 0.64);
    place(group, 'Ícone ' + name, iconFile(iconName), x + (size - s) / 2, y + (size - s) / 2, s);
}

// Pílula (forma + texto centrado).
function pill(group, name, label, cx, y, fontSize, padX, padY, tracking) {
    var probe = text(group, name + ' (texto)', label, FONT_BOLD, fontSize, WHITE, 0, 0, { tracking: tracking });
    var b = probe.bounds;
    var tw = b[2].as('px') - b[0].as('px');
    var th = fontSize * 0.75;
    var w = tw + padX * 2, h = th + padY * 2;
    var l = cx - w / 2;
    shape(group, name + ' (fundo)', l, y, l + w, y + h, h / 2, { gradient: [[CYAN, 0], [DEEP, 4096]], angle: 0 });
    effects({ stroke: 5, shadow: [40, 6, 14] });
    probe.move(group, ElementPlacement.PLACEATBEGINNING);
    b = probe.bounds;
    probe.translate(l + padX - b[0].as('px'), y + padY - b[1].as('px'));
}

// ---------------------------------------------------------------- ecrãs
function brand(group) {
    place(group, 'Marca - ícone', iconFile('gridIcon'), 60, 44, 70);
    effects({ shadow: [45, 4, 10] });
    var t = text(group, 'Marca - texto', 'GRID ROAD GENERATOR', FONT_TITLE, 44, WHITE, 148, 98);
    app.activeDocument.activeLayer = t;
    effects({ shadow: [45, 4, 10] });
}

function modeGroup(parent, index, kind, item) {
    var g = app.activeDocument.layerSets.add();
    g.name = (index < 10 ? '0' : '') + index + (kind == 'PATTERN' ? ' Padrão - ' : ' Área - ') + item[0];
    // Base: marcador para a captura.
    shape(g, 'SCREEN - cola a captura por cima desta camada', 0, 0, W, H, 0, { color: [40, 44, 60] });
    text(g, 'Instrução (esconde)', 'COLA AQUI O SCREEN: ' + item[0].toUpperCase(), FONT_BOLD, 50, [205, 212, 230], W / 2, 520, { center: true });
    text(g, 'Instrução 2 (esconde)', 'Cola a captura (1920 x 1080) numa camada nova por cima de SCREEN e esconde as camadas de instrução.', FONT_MED, 26, [150, 160, 185], W / 2, 580, { center: true });
    // Sombra de leitura no canto inferior esquerdo.
    shape(g, 'Sombra para o texto', 0, 0, W, H, 0, { gradient: [[[20, 12, 50], 0], [[20, 12, 50], 4096]], angle: 45, opacities: [[75, 0], [0, 2800]] });
    // Cartão.
    var x0 = 60, cw = 1060, ch = 300, y0 = H - 60 - ch;
    shape(g, 'Cartão', x0, y0, x0 + cw, y0 + ch, 40, { gradient: [[BLUE, 0], [VIOLET, 4096]], angle: 0 });
    app.activeDocument.activeLayer.fillOpacity = 94;
    effects({ stroke: 5, shadow: [55, 10, 24] });
    iconTile(g, item[0], item[1], x0 + 50, y0 + 60, 170);
    var kindLabel = kind;
    pillLeft(g, 'Tipo', kindLabel, x0 + 258, y0 + 34);
    var title = text(g, 'Título', item[0].toUpperCase(), FONT_TITLE, 76, WHITE, x0 + 258, y0 + 158);
    app.activeDocument.activeLayer = title;
    effects({ shadow: [40, 4, 8] });
    text(g, 'Descrição', item[2], FONT_MED, 29, [245, 240, 255], x0 + 260, y0 + 178, { box: [760, 110], leading: 40 });
    brand(g);
    g.visible = false;
    return g;
}

// Pílula alinhada à esquerda (tipo do modo no cartão).
function pillLeft(group, name, label, x, y) {
    var t = text(group, name + ' (texto)', label, FONT_BOLD, 20, WHITE, 0, 0, { tracking: 250 });
    var b = t.bounds;
    var tw = b[2].as('px') - b[0].as('px');
    var padX = 20, padY = 10, h = 15 + padY * 2;
    shape(group, name + ' (fundo)', x, y, x + tw + padX * 2, y + h, h / 2, { gradient: [[CYAN, 0], [DEEP, 4096]], angle: 0 });
    effects({ stroke: 4 });
    t.move(group, ElementPlacement.PLACEATBEGINNING);
    b = t.bounds;
    t.translate(x + padX - b[0].as('px'), y + padY - b[1].as('px'));
}

function coverGroup() {
    var g = app.activeDocument.layerSets.add();
    g.name = '00 Capa';
    shape(g, 'Fundo gradiente', 0, 0, W, H, 0, { gradient: [[BLUE, 0], [VIOLET, 4096]], angle: 0 });
    place(g, 'Textura (ruas)', ROOT + '/textura.png', 0, 0, null);
    place(g, 'Ícone do mod', iconFile('gridIcon'), (W - 230) / 2, 70, 230);
    effects({ shadow: [50, 8, 16] });
    var t = text(g, 'Título', 'GRID ROAD GENERATOR', FONT_TITLE, 120, WHITE, W / 2, 450, { center: true });
    app.activeDocument.activeLayer = t;
    effects({ shadow: [50, 8, 16] });
    pill(g, 'Pílula', '9 PATTERNS  ·  3 WAYS TO CHOOSE THE AREA', W / 2, 500, 30, 46, 20, 200);
    var size = 150, gap = 42, total = PATTERNS.length * size + (PATTERNS.length - 1) * gap, x = (W - total) / 2;
    for (var i = 0; i < PATTERNS.length; i++) {
        iconTile(g, PATTERNS[i][0], PATTERNS[i][1], x, 640, size);
        text(g, 'Nome ' + PATTERNS[i][0], PATTERNS[i][0].toUpperCase(), FONT_SEMI, 22, WHITE, x + size / 2, 830, { center: true });
        x += size + gap;
    }
    var ax = [430, 900, 1260];
    for (var k = 0; k < AREAS.length; k++) {
        place(g, 'Ícone ' + AREAS[k][0], iconFile(AREAS[k][1]), ax[k], 915, 60);
        text(g, 'Nome ' + AREAS[k][0], AREAS[k][0].toUpperCase(), FONT_SEMI, 30, WHITE, ax[k] + 76, 958);
    }
    return g;
}

// ---------------------------------------------------------------- documento
var doc = app.documents.add(W, H, 72, 'Screenshots', NewDocumentMode.RGB, DocumentFill.TRANSPARENT);
var n = PATTERNS.length + AREAS.length;
for (var a = AREAS.length - 1; a >= 0; a--) modeGroup(null, PATTERNS.length + a + 1, 'AREA', AREAS[a]);
for (var p = PATTERNS.length - 1; p >= 0; p--) modeGroup(null, p + 1, 'PATTERN', PATTERNS[p]);
coverGroup();
// Camada vazia inicial do documento.
try { doc.artLayers[doc.artLayers.length - 1].remove(); } catch (e) {}

var out = new File(ROOT + '/Screenshots.psd');
var opts = new PhotoshopSaveOptions(); opts.layers = true; opts.embedColorProfile = true;
doc.saveAs(out, opts, false, Extension.LOWERCASE);

// Pré-visualizações (capa e um modo) para conferir.
function preview(file) {
    var png = new PNGSaveOptions();
    doc.saveAs(new File(ROOT + '/' + file), png, true, Extension.LOWERCASE);
}
preview('preview_capa.png');
doc.layerSets[0].visible = false;
doc.layerSets[3].visible = true;
preview('preview_modo.png');
doc.layerSets[3].visible = false;
doc.layerSets[0].visible = true;
doc.save();
'ok ' + FONT_TITLE + ' ' + FONT_BOLD + ' ' + FONT_SEMI + ' ' + FONT_MED;


