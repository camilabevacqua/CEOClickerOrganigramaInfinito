using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rubros que el jugador puede elegir al fundar su empresa.
/// Se edita desde el Inspector (agregar, sacar o cambiar textos y colores).
/// </summary>
[CreateAssetMenu(fileName = "Rubros", menuName = "CEO Clicker/Rubros", order = 4)]
public class RubrosConfig : ScriptableObject
{
    [Serializable]
    public class Rubro
    {
        [Tooltip("Identificador único para el guardado. No cambiarlo una vez publicado el juego.")]
        public string id = "rubro";
        [Tooltip("Ej: Pastelería")]
        public string nombre = "Rubro";
        public Color color = Color.gray;
        [Tooltip("Reemplaza el nombre de la primera etapa. Ej: Horneando en un garaje")]
        public string etapaInicial = "";
    }

    public List<Rubro> rubros = new List<Rubro>();
}
