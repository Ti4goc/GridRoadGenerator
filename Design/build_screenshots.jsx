// Gera Screenshots.psd (1920 x 1080) no estilo da Thumbnail: um grupo por modo, texto editável
// (TAN Headline + Montserrat), cartões, pílulas e fundos dos ícones como formas vetoriais com
// estilos de camada, ícones como objetos inteligentes (SVG da pasta icons).
// Photoshop: Ficheiro > Scripts > Procurar... e escolher este ficheiro.
#target photoshop
app.displayDialogs = DialogModes.NO;
app.preferences.rulerUnits = Units.PIXELS;
app.preferences.typeUnits = TypeUnits.PIXELS;

#include "screenshots_lib.jsx"

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


