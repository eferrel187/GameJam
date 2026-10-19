/// <summary>
/// Textos de la historia: introducción, contexto y misión de cada nivel.
/// El contexto se muestra al empezar el nivel y después de cada muerte.
/// </summary>
public static class StoryText
{
    public const string IntroTitle = "Prólogo";

    public const string Intro =
        "Vivíamos tranquilamente en nuestra cabaña en el campo, mi gatito y yo. " +
        "Una noche, mientras dormíamos, unos hombres entraron, me golpearon y se lo llevaron. " +
        "Al despertar, encontré una nota: “Si quieres recuperar a tu gato, tendrás que atravesar la mazmorra”. " +
        "No sabía qué me esperaba allí dentro, pero no iba a abandonarlo. Tomé la nota y salí a buscarlo.";

    public struct Level
    {
        public string title;
        public string context;
        public string mission;
    }

    /// Devuelve false si la escena no tiene texto (tutorial, menú...).
    public static bool TryGet(string scene, out Level level)
    {
        level = default;
        switch (scene)
        {
            case "Nivel1":
                level.title = "Nivel 1 · El comienzo de la búsqueda";
                level.context = "Se llevaron a mi gatito. La nota decía que debía atravesar esta mazmorra para recuperarlo. Tengo que seguir adelante.";
                level.mission = "Misión: consigue la llave y abre la puerta. Cuidado con el suelo.";
                return true;
            case "Nivel2":
                level.title = "Nivel 2 · Los rodillos";
                level.context = "Más adentro, el pasillo se estrecha. Hay rodillos con púas girando entre las sombras y una pared que parece a punto de ceder. Mi gatito tiene que estar más allá.";
                level.mission = "Misión: empuja la pared (E), consigue la llave y sal sin tocar los rodillos. Agáchate con C.";
                return true;
            case "Nivel3":
                level.title = "Nivel 3 · Dos caminos";
                level.context = "Dos caminos y dos llaves, pero la puerta solo acepta una. El suelo esconde púas y hay cepos entre las sombras. Debo elegir bien antes de girar la llave.";
                level.mission = "Misión: encuentra la llave verdadera y abre la puerta.";
                return true;
            case "Nivel4":
                level.title = "Nivel 4 · Trampas y llaves engañosas";
                level.context = "Hay dos llaves entre estas celdas, pero solo una abre el camino. Debo observar el suelo y tener cuidado: algo parece estar esperando a que cometa un error.";
                level.mission = "Misión: elige la llave correcta y cruza la salida. Los jarrones rotos y las calaveras marcan el peligro.";
                return true;
            case "Nivel5":
                level.title = "Nivel 5 · La biblioteca";
                level.context = "La biblioteca guarda el camino hacia mi gatito. Debo encontrar tres papeles entre los libros y descifrar hacia dónde deben mirar las cuatro estatuas. Algunas estanterías esconden algo más que secretos.";
                level.mission = "Misión: revisa las estanterías (E), reúne las 3 pistas y gira las 4 estatuas (E). Releer pistas: Tab.";
                return true;
            case "Nivel6":
                level.title = "Nivel 6 · El rescate";
                level.context = "¡Ahí está mi gatito! La jaula tiene tres cerraduras y las llaves están repartidas por la sala. Algo está saliendo de esa puerta… Tengo dos minutos para liberarlo antes de que se lo lleve.";
                level.mission = "Misión: reúne las 3 llaves y abre la jaula (E) antes de 02:00. No caigas de los puentes y huye de las serpientes.";
                return true;
        }
        return false;
    }

    // ---------------------------------------------------------- Nivel 5
    public static readonly string[] Clues =
    {
        "La primera saluda al sol cuando nace; la última lo despide cuando se oculta.",
        "La segunda mira al norte, donde el frío guarda silencio.",
        "La tercera da la espalda al norte.",
    };

    // ---------------------------------------------------------- Epílogo
    public const string Epilogue =
        "Salimos de aquella mazmorra y volvimos a nuestra pequeña cabaña. " +
        "Después de tanto miedo, escuchar su ronroneo era todo lo que necesitaba. " +
        "Estábamos juntos, estábamos a salvo… y nos quedaban muchas siestas al sol.";

    public const string TheEnd = "Fin. Hogar es donde está tu gatito.";
}
