using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pestañas de abajo (Personal / Departamentos / Empresa): muestra un panel por vez.
/// </summary>
public class UIPestanas : MonoBehaviour
{
    [Serializable]
    public class Pestana
    {
        public Button boton;
        public GameObject panel;
    }

    [SerializeField] private Pestana[] pestanas = new Pestana[0];
    [SerializeField] private int pestanaInicial = 0;

    [Header("Colores")]
    [SerializeField] private Color colorSeleccionado = new Color32(0x13, 0x20, 0x3A, 0xFF);
    [SerializeField] private Color colorNormal = new Color32(0xE7, 0xEC, 0xF2, 0xFF);
    [SerializeField] private Color textoSeleccionado = Color.white;
    [SerializeField] private Color textoNormal = new Color32(0x13, 0x20, 0x3A, 0xFF);

    public int Actual { get; private set; } = -1;

    private void Start()
    {
        for (int i = 0; i < pestanas.Length; i++)
        {
            int indice = i; // copia local para el lambda
            if (pestanas[i].boton != null)
            {
                pestanas[i].boton.onClick.AddListener(() => Seleccionar(indice));
            }
        }

        Seleccionar(pestanaInicial);
    }

    public void Seleccionar(int indice)
    {
        if (indice < 0 || indice >= pestanas.Length) return;
        Actual = indice;

        for (int i = 0; i < pestanas.Length; i++)
        {
            bool activa = i == indice;
            Pestana p = pestanas[i];

            if (p.panel != null) p.panel.SetActive(activa);

            if (p.boton != null)
            {
                if (p.boton.targetGraphic != null)
                {
                    p.boton.targetGraphic.color = activa ? colorSeleccionado : colorNormal;
                }

                TMP_Text texto = p.boton.GetComponentInChildren<TMP_Text>();
                if (texto != null) texto.color = activa ? textoSeleccionado : textoNormal;
            }
        }
    }
}
