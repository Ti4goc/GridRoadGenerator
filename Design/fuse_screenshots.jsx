// Acrescenta a Screenshots.psd dois ecrãs "fundidos" (limite de 10 capturas do Paradox Mods), com o
// mesmo layout dos ecrãs de um só modo — captura a toda a altura, sombra de leitura, ícone, pílula,
// título e descrição em baixo à esquerda — mas com o ecrã dividido em faixas e o texto mais pequeno:
//  - "13 Áreas - fusão" : as 3 maneiras de escolher a área, em 3 faixas ;
//  - "14 Padrões - Concentric + Radial" : os dois padrões de anéis, em 2 faixas.
// Usa as capturas já coladas nos grupos 08 a 12 (copiadas, os grupos originais ficam intactos).
// Texto editável, cartões e fundos como formas, como em build_screenshots.jsx.
#target photoshop
app.displayDialogs = DialogModes.NO;
app.preferences.rulerUnits = Units.PIXELS;
app.preferences.typeUnits = TypeUnits.PIXELS;

#include "screenshots_lib.jsx"

var doc = null;
for (var d = 0; d < app.documents.length; d++) if (app.documents[d].name == 'Screenshots.psd') doc = app.documents[d];
if (!doc) doc = app.open(new File(ROOT + '/Screenshots.psd'));
app.activeDocument = doc;

function generatedLayer(name) {
    return /^(SCREEN|Instrução|Sombra para o texto|Cartão|Fundo do ícone|Ícone|Tipo|Título|Descrição|Marca|Fundo gradiente|Textura|Nome |Pílula)/.test(name);
}

function groupByPrefix(prefix) {
    for (var g = 0; g < doc.layerSets.length; g++) if (doc.layerSets[g].name.indexOf(prefix) == 0) return doc.layerSets[g];
    return null;
}

// Captura colada pelo utilizador num grupo (primeira camada que não foi gerada pelo script).
function shotOf(prefix) {
    var group = groupByPrefix(prefix);
    if (!group) return null;
    for (var l = 0; l < group.layers.length; l++) if (!generatedLayer(group.layers[l].name)) return group.layers[l];
    return null;
}

// Versão anterior do ecrã : guardada, escondida e renomeada (fora da exportação), nunca apagada.
function removeGroup(prefix) {
    var g = groupByPrefix(prefix);
    if (!g) return;
    try { g.allLocked = false; } catch (e) {}
    g.visible = false;
    g.name = 'antigo - ' + g.name;
}

// Captura recortada (máscara de recorte) num retângulo arredondado.
function framedShot(group, name, source, x, y, w, h, radius) {
    var frame = shape(group, name + ' (moldura)', x, y, x + w, y + h, radius, { color: [30, 30, 40] });
    if (radius > 0) effects({ stroke: 5, shadow: [45, 8, 18] });
    if (!source) return;
    var copy = source.duplicate(group, ElementPlacement.PLACEATBEGINNING);
    // Mesmo por cima da moldura (senão a máscara de recorte apanhava outra camada, ex. o texto da pílula).
    copy.move(frame, ElementPlacement.PLACEBEFORE);
    copy.name = name;
    copy.visible = true;
    doc.activeLayer = copy;
    var b = copy.bounds;
    var sw = b[2].as('px') - b[0].as('px'), sh = b[3].as('px') - b[1].as('px');
    var s = Math.max(w / sw, h / sh) * 100;
    copy.resize(s, s, AnchorPosition.MIDDLECENTER);
    b = copy.bounds;
    copy.translate(x + w / 2 - (b[0].as('px') + b[2].as('px')) / 2, y + h / 2 - (b[1].as('px') + b[3].as('px')) / 2);
    copy.grouped = true;
}

// Uma faixa : captura a toda a altura, sombra de leitura e o bloco de texto dos ecrãs de um só modo,
// à escala `k` (1 = tamanho dos ecrãs de um só modo).
function slice(group, kind, item, source, x, w, k) {
    framedShot(group, 'Captura ' + item[0], source, x, 0, w, H, 0);
    shape(group, 'Sombra para o texto ' + item[0], x, 0, x + w, H, 0,
        { gradient: [[[20, 12, 50], 0], [[20, 12, 50], 4096]], angle: 90, opacities: [[85, 0], [0, 2600]] });
    // Mesmas proporções que os ecrãs de um só modo (ícone 170, pílula 24 px acima do ícone, título
    // 76 px com a base a +99, descrição a +121 do topo do ícone), à escala k.
    var left = x + Math.round(40 * k), icon = Math.round(170 * k);
    var textX = left + icon + Math.round(28 * k);
    var descSize = Math.max(18, Math.round(29 * k));
    var descH = Math.round(3 * descSize * 1.35);
    var top = H - 40 - Math.max(icon, Math.round(121 * k) + descH);
    iconTile(group, item[0], item[1], left, top, icon);
    pillScaled(group, 'Tipo ' + item[0], kind, textX, top - Math.round(24 * k), k);
    var titleSize = Math.round(76 * k);
    var maxRight = x + w - Math.round(30 * k);
    var t = text(group, 'Título ' + item[0], item[0].toUpperCase(), FONT_TITLE, titleSize, WHITE, textX, top + Math.round(99 * k));
    // Título reduzido até caber na faixa.
    while (t.bounds[2].as('px') > maxRight && titleSize > 20) {
        titleSize -= 2;
        t.textItem.size = titleSize;
    }
    doc.activeLayer = t;
    effects({ shadow: [40, 4, 8] });
    text(group, 'Descrição ' + item[0], item[2], FONT_MED, descSize, [245, 240, 255], textX, top + Math.round(121 * k),
        { box: [maxRight - textX, descH + descSize], leading: Math.round(descSize * 1.35) });
}

// Pílula do tipo (AREA / PATTERN) à escala k, alinhada à esquerda.
function pillScaled(group, name, label, x, y, k) {
    var size = Math.max(13, Math.round(20 * k));
    var t = text(group, name + ' (texto)', label, FONT_BOLD, size, WHITE, 0, 0, { tracking: 250 });
    var b = t.bounds;
    var tw = b[2].as('px') - b[0].as('px');
    var padX = Math.round(20 * k), padY = Math.round(10 * k), h = Math.round(size * 0.75) + padY * 2;
    shape(group, name + ' (fundo)', x, y, x + tw + padX * 2, y + h, h / 2, { gradient: [[CYAN, 0], [DEEP, 4096]], angle: 0 });
    effects({ stroke: Math.max(3, Math.round(4 * k)) });
    t.move(group, ElementPlacement.PLACEATBEGINNING);
    b = t.bounds;
    t.translate(x + padX - b[0].as('px'), y + padY - b[1].as('px'));
}

// Filetes brancos entre as faixas.
function dividers(group, count) {
    for (var d2 = 1; d2 < count; d2++) {
        var x = Math.round(W * d2 / count);
        shape(group, 'Divisória ' + d2, x - 3, 0, x + 3, H, 0, { color: WHITE });
        effects({ shadow: [45, 0, 12] });
    }
}

function splitScreen(name, kind, items, shots, scale) {
    var g = doc.layerSets.add();
    g.name = name;
    var w = Math.round(W / items.length);
    for (var n2 = 0; n2 < items.length; n2++) slice(g, kind, items[n2], shots[n2], n2 * w, n2 == items.length - 1 ? W - n2 * w : w, scale);
    dividers(g, items.length);
    brand(g);
    return g;
}

removeGroup('13 Áreas');
var areas = splitScreen('13 Áreas - fusão', 'AREA', AREAS, [shotOf('10 '), shotOf('11 '), shotOf('12 ')], 0.62);
removeGroup('14 Padrões');
var rings = splitScreen('14 Padrões - Concentric + Radial', 'PATTERN', [PATTERNS[7], PATTERNS[8]], [shotOf('08 '), shotOf('09 ')], 0.8);

// Só o ecrã das áreas fica visível para conferir ; os outros escondidos.
for (var v = 0; v < doc.layerSets.length; v++) doc.layerSets[v].visible = (doc.layerSets[v] == areas);
doc.save();
var png = new PNGSaveOptions();
doc.saveAs(new File(ROOT + '/preview_areas.png'), png, true, Extension.LOWERCASE);
areas.visible = false; rings.visible = true;
doc.saveAs(new File(ROOT + '/preview_aneis.png'), png, true, Extension.LOWERCASE);
'ok';
