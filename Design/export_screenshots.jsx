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

var groups = [];
for (var g = 0; g < doc.layerSets.length; g++) groups.push(doc.layerSets[g]);
// Ordem do painel é de cima para baixo: 00 Capa primeiro.
groups.sort(function (a, b) { return a.name < b.name ? -1 : 1; });

var saved = [], skipped = [];
var visibility = [];
for (var v = 0; v < groups.length; v++) visibility.push(groups[v].visible);

var n = 0;
for (var k = 0; k < groups.length; k++) {
    var group = groups[k];
    var hasShot = group.name.indexOf('00 ') == 0;
    for (var l = 0; l < group.layers.length; l++) if (!generated(group.layers[l].name)) hasShot = true;
    if (!hasShot) { skipped.push(group.name); continue; }
    for (var o = 0; o < groups.length; o++) groups[o].visible = (o == k);
    var hidden = [];
    for (var h = 0; h < group.layers.length; h++) {
        var layer = group.layers[h];
        if (isHelper(layer.name) && layer.visible && group.name.indexOf('00 ') != 0) { layer.visible = false; hidden.push(layer); }
    }
    n++;
    var file = new File(OUT.fsName + '/' + (n < 10 ? '0' : '') + n + '.jpg');
    var jpg = new JPEGSaveOptions(); jpg.quality = 11; jpg.embedColorProfile = true;
    doc.saveAs(file, jpg, true, Extension.LOWERCASE);
    saved.push(file.name + ' <- ' + group.name);
    for (var r = 0; r < hidden.length; r++) hidden[r].visible = true;
}
for (var w = 0; w < groups.length; w++) groups[w].visible = visibility[w];
'exportados:\n' + saved.join('\n') + '\nsaltados (sem captura):\n' + skipped.join('\n');
