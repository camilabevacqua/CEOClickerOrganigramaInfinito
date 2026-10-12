using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de etapas de la empresa según lo facturado en la partida actual.
/// Se edita desde el Inspector. Mantener ordenadas de menor a mayor "mínimo facturado".
/// </summary>
[CreateAssetMenu(fileName = "Etapas", menuName = "CEO Clicker/Etapas", order = 2)]
public class EtapasConfig : ScriptableObject
{
    [Serializable]
    public class Etapa
    {
        [Tooltip("Ej: Freelancer en un garaje")]
        public string nombreEtapa = "Etapa";
        [Tooltip("Ej: Garaje Unipersonal")]
        public string nombreEmpresa = "Empresa";
        [Tooltip("Facturación de la partida necesaria para llegar a esta etapa.")]
        public double minimoFacturado = 0;
    }

    [Tooltip("De menor a mayor. La primera debería tener mínimo 0.")]
    public List<Etapa> etapas = new List<Etapa>();
}
