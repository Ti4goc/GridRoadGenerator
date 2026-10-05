// Exporta cada grupo de Screenshots.psd para JPG em ../Properties (01.jpg, 02.jpg, ...).
// Grava primeiro o PSD. Grupos de modo sem captura colada são saltados; nos outros, as camadas
// SCREEN e de instrução ficam escondidas na exportação.
#target photoshop
app.displayDialogs = DialogModes.NO;
var ROOT = new File($.fileName).parent.fsName;
var OUT = new Folder(ROOT + '/../Properties');

var doc = null;
for (var i = 0; i < app.documents.length; i++) if (app.documents[i].name == 'Screenshots.psd') doc = app.documents[i];
if (!doc) doc = app.open(new File(ROOT + '/Screenshots.psd'));
app.activeDocument = doc;
doc.save();

function generated(name) {
    return /^(SCREEN|Instrução|Sombra para o texto|Cartão|Fundo do ícone|Ícone|Tipo|Título|Descrição|Marca|Fundo gradiente|Textura|Nome |Pílula)/.test(name);
}
function isHelper(name) { return /^(SCREEN|Instrução)/.test(name); }

// Ecrãs publicados, por esta ordem (Paradox Mods aceita no máximo 10 capturas) : os grupos 08 a 12
// só servem de fonte aos ecrãs fundidos 13 e 14 (ver fuse_screenshots.jsx).
var ORDER = ['00 ', '13 ', '01 ', '02 ', '03 ', '04 ', '05 ', '06 ', '07 ', '14 '];
var groups = [];
for (var o = 0; o < ORDER.length; o++)
    for (var g = 0; g < doc.layerSets.length; g++)
        if (doc.layerSets[g].name.indexOf(ORDER[o]) == 0) groups.push(doc.layerSets[g]);
var all = [];
for (var g2 = 0; g2 < doc.layerSets.length; g2++) all.push(doc.layerSets[g2]);

var saved = [], skipped = [];
var visibility = [];
for (var v = 0; v < all.length; v++) visibility.push(all[v].visible);

var n = 0;
for (var k = 0; k < groups.length; k++) {
    var group = groups[k];
    var hasShot = /^(00|13|14) /.test(group.name);
    for (var l = 0; l < group.layers.length; l++) if (!generated(group.layers[l].name)) hasShot = true;
    if (!hasShot) { skipped.push(group.name); continue; }
    for (var o2 = 0; o2 < all.length; o2++) all[o2].visible = (all[o2] == group);
    var hidden = [];
    for (var h = 0; h < group.layers.length; h++) {
        var layer = group.layers[h];
        if (isHelper(layer.name) && layer.visible && !/^(00|13|14) /.test(group.name)) { layer.visible = false; hidden.push(layer); }
    }
    n++;
    var file = new File(OUT.fsName + '/' + (n < 10 ? '0' : '') + n + '.jpg');
    var jpg = new JPEGSaveOptions(); jpg.quality = 11; jpg.embedColorProfile = true;
    doc.saveAs(file, jpg, true, Extension.LOWERCASE);
    saved.push(file.name + ' <- ' + group.name);
    for (var r = 0; r < hidden.length; r++) hidden[r].visible = true;
}
for (var w = 0; w < all.length; w++) all[w].visible = visibility[w];
'exportados:\n' + saved.join('\n') + '\nsaltados (sem captura):\n' + skipped.join('\n');
