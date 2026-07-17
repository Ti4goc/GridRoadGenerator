// Pattern de bindings adapté de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import { bindValue, trigger } from "cs2/api";
import { Entity } from "cs2/utils";
import mod from "mod.json";

/// Entrée du sélecteur de réseau (miroir de GridRoadUISystem.WritePrefabEntry).
export type PrefabEntry = {
    Entity: Entity;
    Name: string;
    Icon: string;
};

// Valeurs poussées par GridRoadUISystem (C#). Les noms doivent correspondre.
export const toolActive$ = bindValue<boolean>(mod.id, "TOOL_ACTIVE", false);
export const nodeCount$ = bindValue<number>(mod.id, "NODE_COUNT", 0);
export const canApply$ = bindValue<boolean>(mod.id, "CAN_APPLY", false);
export const perimeterInvalid$ = bindValue<boolean>(mod.id, "PERIMETER_INVALID", false);
export const mode$ = bindValue<number>(mod.id, "MODE", 0);
export const columns$ = bindValue<number>(mod.id, "COLUMNS", 3);
export const rows$ = bindValue<number>(mod.id, "ROWS", 3);
export const spacing$ = bindValue<number>(mod.id, "SPACING", 60);
export const roadPrefabName$ = bindValue<string>(mod.id, "ROAD_PREFAB_NAME", "");
export const roadPrefabIcon$ = bindValue<string>(mod.id, "ROAD_PREFAB_ICON", "");
export const roadPrefabAuto$ = bindValue<boolean>(mod.id, "ROAD_PREFAB_AUTO", true);
export const anarchyAvailable$ = bindValue<boolean>(mod.id, "ANARCHY_AVAILABLE", false);
export const pickerType$ = bindValue<number>(mod.id, "PICKER_TYPE", 0);
export const pickerData$ = bindValue<PrefabEntry[]>(mod.id, "PICKER_DATA", []);
export const recentPrefabs$ = bindValue<PrefabEntry[]>(mod.id, "RECENT_PREFABS", []);

// Déclencheurs vers le C# (synchronisés avec Options > Mods côté C#).
export const setMode = (value: number) => trigger(mod.id, "SET_MODE", value);
export const setColumns = (value: number) => trigger(mod.id, "SET_COLUMNS", value);
export const setRows = (value: number) => trigger(mod.id, "SET_ROWS", value);
export const setSpacing = (value: number) => trigger(mod.id, "SET_SPACING", value);
export const generateGrid = () => trigger(mod.id, "GENERATE");
export const clearSelection = () => trigger(mod.id, "CLEAR_SELECTION");
export const toggleTool = () => trigger(mod.id, "TOGGLE_TOOL");
export const setPickerType = (value: number) => trigger(mod.id, "SET_PICKER_TYPE", value);

// Bindings du mod Anarchy lui-même (groupe "Anarchy", noms relevés dans son
// bundle UI). Lus/déclenchés uniquement quand ANARCHY_AVAILABLE est vrai :
// sur un binding absent, bindValue reste simplement à sa valeur de repli.
export const anarchyEnabled$ = bindValue<boolean>("Anarchy", "AnarchyEnabled", false);
export const toggleAnarchy = () => trigger("Anarchy", "AnarchyToggled");
export const pickPrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB", entity);
export const pickAuto = () => trigger(mod.id, "PICK_AUTO");
