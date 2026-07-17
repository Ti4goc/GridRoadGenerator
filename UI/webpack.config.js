// Config webpack adaptée de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
const path = require("path");
const MOD = require("./mod.json");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const { RawSource } = require("webpack").sources;

const CSII_USERDATAPATH = process.env.CSII_USERDATAPATH;
if (!CSII_USERDATAPATH) {
    throw "CSII_USERDATAPATH environment variable is not set, ensure the CSII Modding Toolchain is installed correctly";
}

// Sortie directement dans le dossier de déploiement du mod (à côté de la DLL).
const OUTPUT_DIR = `${CSII_USERDATAPATH}\\Mods\\${MOD.id}`;

// Injecte `export const hasCSS = true/false` dans le .mjs pour que le loader UI du
// jeu sache s'il doit aussi charger le .css (plugin repris de CS2-NetworkTools, MIT).
class CSSPresencePlugin {
    apply(compiler) {
        compiler.hooks.compilation.tap("CSSPresencePlugin", (compilation) => {
            compilation.hooks.processAssets.tap(
                { name: "CSSPresencePlugin", stage: compilation.PROCESS_ASSETS_STAGE_ADDITIONS },
                () => {
                    const hasCSS = Object.keys(compilation.assets).some((a) => a.endsWith(".css"));
                    for (const chunk of compilation.chunks) {
                        for (const file of chunk.files) {
                            if (!file.endsWith(".mjs")) continue;
                            const asset = compilation.getAsset(file);
                            const source = asset.source.source();
                            const updated = source.replace(
                                "export {",
                                `const hasCSS = ${hasCSS}; export { hasCSS, `
                            );
                            compilation.updateAsset(file, new RawSource(updated));
                        }
                    }
                }
            );
        });
    }
}

module.exports = {
    mode: "production",
    stats: { errorDetails: true, children: true },
    entry: {
        [MOD.id]: "./src/index.tsx",
    },
    devtool: false,
    externalsType: "window",
    externals: {
        react: "React",
        "react-dom": "ReactDOM",
        "cs2/modding": "cs2/modding",
        "cs2/api": "cs2/api",
        "cs2/bindings": "cs2/bindings",
        "cs2/l10n": "cs2/l10n",
        "cs2/ui": "cs2/ui",
        "cs2/input": "cs2/input",
        "cs2/utils": "cs2/utils",
        "cohtml/cohtml": "cohtml/cohtml",
    },
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: "ts-loader",
                exclude: /node_modules/,
            },
            {
                // Icônes embarquées dans le module UI : émises dans le dossier déployé
                // et servies par l'hôte coui://ui-mods/ (publicPath ci-dessous).
                test: /\.(svg|png|jpg|gif)$/,
                type: "asset/resource",
                generator: { filename: "images/[name][ext]" },
            },
            {
                test: /\.s?css$/,
                include: path.join(__dirname, "src"),
                use: [
                    MiniCssExtractPlugin.loader,
                    {
                        loader: "css-loader",
                        options: {
                            url: true,
                            importLoaders: 1,
                            modules: {
                                auto: true,
                                exportLocalsConvention: "camelCase",
                                localIdentName: "[local]_[hash:base64:3]",
                            },
                        },
                    },
                    "sass-loader",
                ],
            },
        ],
    },
    resolve: {
        extensions: [".tsx", ".ts", ".js"],
        modules: ["node_modules", path.join(__dirname, "src")],
        alias: {
            "mod.json": path.resolve(__dirname, "mod.json"),
        },
    },
    output: {
        path: path.resolve(__dirname, OUTPUT_DIR),
        filename: "[name].mjs",
        library: {
            type: "module",
        },
        publicPath: "coui://ui-mods/",
    },
    experiments: {
        outputModule: true,
    },
    plugins: [new MiniCssExtractPlugin(), new CSSPresencePlugin()],
};
