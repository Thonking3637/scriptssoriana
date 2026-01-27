using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class PanelTableroRegistro : MonoBehaviour
{
    [Header("Referencia de guía")]
    public SemanaGuiaSO guiaSemana;

    [Header("Celdas del tablero (todas las del canvas)")]
    public List<CeldaRegistroUI> celdas = new List<CeldaRegistroUI>();

    private struct ParcialDia
    {
        public string producto;
        public int kg;
        public bool? registrado;
        public int slotIndex; // soporte multi-slot por día
    }

    // Key: Día + índice de caso
    private readonly Dictionary<(string, int), ParcialDia> progreso = new();
    private readonly HashSet<(string, int)> casosCompletados = new();

    public Action<SemanaGuiaSO.CasoDia> OnCasoCompletado;

    private void Awake()
    {
        foreach (var celda in celdas)
        {
            if (celda == null) continue;

            // Antes: RegistrarCelda(tipo, valor, dia) → usaba “primer caso libre”
            // Ahora: pasamos la CELDA para usar su dia/slotIndex reales.
            celda.OnStickerColocado.AddListener((tipo, valor, _diaIgnorado) =>
            {
                RegistrarCeldaDesdeCelda(celda, tipo, valor);
            });
        }
    }

    // ======================================================================================
    // REGISTRO DE CELDAS
    // ======================================================================================
    public void RegistrarCelda(string tipo, string valor, string dia)
    {
        if (guiaSemana == null || string.IsNullOrEmpty(dia)) return;

        // Encontrar el caso más adecuado para este día (primero no completado)
        int slotIndex = GetPrimerCasoLibreIndex(dia);
        var key = (dia.Trim(), slotIndex);

        if (!progreso.TryGetValue(key, out var p))
            p = new ParcialDia { producto = "", kg = 0, registrado = null, slotIndex = slotIndex };

        switch (tipo)
        {
            case "Producto": p.producto = NormalizeProducto(valor); break;
            case "Kg": p.kg = ParseKg(valor); break;
            case "Registro": p.registrado = ParseRegistro(valor); break;
        }

        progreso[key] = p;
        TryResolverCasoParaDia(dia, slotIndex, p);
    }

    private void RegistrarCeldaDesdeCelda(CeldaRegistroUI celda, string tipo, string valor)
    {
        if (guiaSemana == null || celda == null) return;

        string dia = celda.dia?.Trim();
        int slotIndex = celda.slotIndex;
        var key = (dia, slotIndex);

        if (!progreso.TryGetValue(key, out var p))
            p = new ParcialDia { producto = "", kg = 0, registrado = null, slotIndex = slotIndex };

        switch (tipo)
        {
            case "Producto": p.producto = NormalizeProducto(valor); break;
            case "Kg": p.kg = ParseKg(valor); break;
            case "Registro": p.registrado = ParseRegistro(valor); break;
        }

        progreso[key] = p;
        TryResolverCasoParaDia(dia, slotIndex, p);
    }




    // ======================================================================================
    // VALIDACIÓN DE CASO
    // ======================================================================================
    private void TryResolverCasoParaDia(string dia, int slotIndex, ParcialDia p)
    {
        if (guiaSemana == null || string.IsNullOrEmpty(dia)) return;
        if (p.registrado == null) return;

        var caso = GetCasoByDiaSlot(dia, slotIndex);
        if (caso == null) return;

        var prodCaso = NormalizeProducto(caso.producto);
        bool matchProducto = string.Equals(prodCaso, p.producto, StringComparison.OrdinalIgnoreCase);
        bool matchKg = caso.kg == p.kg;
        bool matchReg = caso.registradoApp == (p.registrado ?? false);

        if (matchProducto && matchKg && matchReg && !casosCompletados.Contains((dia, slotIndex)))
        {
            casosCompletados.Add((dia, slotIndex));
            OnCasoCompletado?.Invoke(caso);

            // 🔊 sonido
            SoundManager.Instance?.PlaySound("success");

            // 🔒 bloquear SOLO las 3 celdas del caso correcto (día + slot)
            var celdasDia = celdas.Where(c => c.dia == dia && c.slotIndex == slotIndex).ToList();
            foreach (var c in celdasDia) c.Lock();
        }
    }

    // ======================================================================================
    // HELPERS
    // ======================================================================================
    private SemanaGuiaSO.CasoDia GetCasoByDiaSlot(string dia, int slot)
    {
        if (guiaSemana == null) return null;
        var casosDia = guiaSemana.casos.Where(c => SameDia(c.dia, dia)).ToList();
        if (slot >= 0 && slot < casosDia.Count) return casosDia[slot];
        return null;
    }

    private int GetPrimerCasoLibreIndex(string dia)
    {
        if (guiaSemana == null) return 0;
        var casosDia = guiaSemana.casos.Where(c => SameDia(c.dia, dia)).ToList();
        for (int i = 0; i < casosDia.Count; i++)
        {
            if (!casosCompletados.Contains((dia, i))) return i;
        }
        return 0; // fallback
    }

    private static string NormalizeProducto(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return v.Trim().ToUpperInvariant();
    }

    private static int ParseKg(string v)
    {
        if (string.IsNullOrEmpty(v)) return 0;
        var s = v.Trim().ToLowerInvariant();
        s = s.Replace("kg", "").Replace("k", "").Replace(" ", "");
        if (float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out float f))
            return Mathf.RoundToInt(f);
        if (int.TryParse(s, out int i)) return i;
        return 0;
    }

    private static bool ParseRegistro(string v)
    {
        if (string.IsNullOrEmpty(v)) return false;
        var s = v.Trim().ToLowerInvariant();
        return s == "si" || s == "sí" || s == "yes";
    }

    private static bool SameDia(string a, string b)
    {
        if (a == null || b == null) return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public int TotalCasos() => guiaSemana ? guiaSemana.casos.Count : 0;
    public int Completados() => casosCompletados.Count;

    public void ResetBoard()
    {
        progreso.Clear();
        casosCompletados.Clear();
        foreach (var c in celdas)
        {
            c.Unlock();
            if (c.texto) c.texto.text = "";
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Validar tablero (día/slot)")]
    private void ValidarTablero()
    {
        var grupos = celdas
            .GroupBy(c => (c.dia?.Trim() ?? "", c.slotIndex))
            .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2);

        foreach (var g in grupos)
        {
            var tipos = g.Select(c => c.tipo).Distinct().ToList();
            if (tipos.Count != 3)
                Debug.LogWarning($"[Tablero] Grupo incompleto -> Día '{g.Key.Item1}' slot {g.Key.Item2} tiene {tipos.Count}/3 tipos ({string.Join(", ", tipos)})", this);
        }

        // Checa que la guía tenga suficientes slots por día
        if (guiaSemana)
        {
            var porDia = guiaSemana.casos.GroupBy(c => c.dia?.Trim() ?? "");
            foreach (var g in grupos)
            {
                var totalSlots = porDia.FirstOrDefault(d => string.Equals(d.Key, g.Key.Item1, StringComparison.OrdinalIgnoreCase))?.Count() ?? 0;
                if (g.Key.Item2 >= totalSlots)
                    Debug.LogWarning($"[Tablero] Día '{g.Key.Item1}' slot {g.Key.Item2} no existe en la guía (solo {totalSlots} casos).", this);
            }
        }

        Debug.Log("[Tablero] Validación terminada.");
    }
#endif
}
