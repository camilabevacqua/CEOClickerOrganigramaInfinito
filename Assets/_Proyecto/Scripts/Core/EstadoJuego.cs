using System;

/// <summary>
/// Todos los datos de la partida. Solo datos, sin lógica.
/// Es lo que más adelante se guarda como JSON.
/// </summary>
[Serializable]
public class EstadoJuego
{
    // Plata disponible para gastar
    public double plata;

    // Lo facturado desde la última salida a la bolsa (define etapas y acciones a ganar)
    public double facturadoPartida;

    // Todo lo facturado en la historia (no se reinicia nunca)
    public double facturadoHistorico;

    // Cantidad de veces que se tocó "Facturar" (Tratos). No se reinicia.
    public long clicsTotales;

    // Acciones ganadas al salir a la bolsa (prestigio). No se reinician.
    public int acciones;

    // Cantidad contratada de cada puesto, en el mismo orden que la lista de puestos del GameManager
    public int[] cantidadesPuestos = new int[0];

    // Nombre que el jugador le puso a su empresa (vacío = todavía no la fundó)
    public string nombreEmpresa = "";

    // Rubro elegido al fundar la empresa (id de RubrosConfig)
    public string rubroId = "";

    // Nivel de cada departamento, en el mismo orden que la lista de departamentos del GameManager
    public int[] nivelesDepartamentos = new int[0];

    // Personal de cada puesto interno de los departamentos (Reclutadores, Capacitadores...),
    // en el orden en que el GameManager recorre departamentos y sus roles
    public int[] personalRoles = new int[0];
}
