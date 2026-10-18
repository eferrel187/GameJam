using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class HazardCylinderNivel2 : MonoBehaviour
{
    public enum Comportamiento
    {
        PasilloIdaYVuelta,
        Fijo,
        CuartoHastaPared
    }

    [Header("Tipo de cilindro")]
    public Comportamiento comportamiento = Comportamiento.Fijo;

    [Header("Destinos (objetos vacios en la escena)")]
    public Transform puntoParedInicio;
    public Transform puntoPuerta;
    public Transform puntoParedCuarto;

    [Header("Movimiento")]
    [Min(0f)] public float esperaInicial = 6f;
    [Min(0.1f)] public float velocidad = 3f;
    public Vector3 ejeGiro = Vector3.up;
    public float gradosPorSegundo = 180f;

    private enum Fase { Esperando, Quieto, HaciaPared, HaciaPuerta, Terminado }
    private Fase fase;
    private Rigidbody rb;
    private Vector3 posicionOriginal;
    private Quaternion rotacionOriginal;
    private float esperaRestante;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        GetComponent<BoxCollider>().isTrigger = true;

        posicionOriginal = transform.position;
        rotacionOriginal = transform.rotation;
    }

    private void Start()
    {
        Reiniciar();
    }

    public void Reiniciar()
    {
        gameObject.SetActive(true);
        rb.position = posicionOriginal;
        rb.rotation = rotacionOriginal;
        transform.SetPositionAndRotation(posicionOriginal, rotacionOriginal);
        Physics.SyncTransforms();

        esperaRestante = esperaInicial;
        fase = comportamiento == Comportamiento.PasilloIdaYVuelta
            ? Fase.Esperando
            : Fase.Quieto;
    }

    public void ActivarAlEntrarCuarto()
    {
        if (comportamiento == Comportamiento.CuartoHastaPared &&
            fase == Fase.Quieto)
        {
            fase = Fase.HaciaPared;
        }
    }

    private void FixedUpdate()
    {
        if (fase == Fase.Quieto || fase == Fase.Terminado)
            return;

        if (fase == Fase.Esperando)
        {
            esperaRestante -= Time.fixedDeltaTime;
            if (esperaRestante > 0f)
                return;
            fase = Fase.HaciaPared;
        }

        Transform destino = null;

        if (fase == Fase.HaciaPared)
            destino = comportamiento == Comportamiento.PasilloIdaYVuelta
                ? puntoParedInicio : puntoParedCuarto;
        else if (fase == Fase.HaciaPuerta)
            destino = puntoPuerta;

        if (destino == null)
        {
            Debug.LogWarning("Falta asignar un punto de destino en " + name, this);
            fase = Fase.Quieto;
            return;
        }

        Vector3 siguiente = Vector3.MoveTowards(
            rb.position, destino.position, velocidad * Time.fixedDeltaTime);

        rb.MovePosition(siguiente);

        if (ejeGiro.sqrMagnitude > 0.0001f)
        {
            Quaternion giro = Quaternion.AngleAxis(
                gradosPorSegundo * Time.fixedDeltaTime,
                ejeGiro.normalized);
            rb.MoveRotation(rb.rotation * giro);
        }

        if (Vector3.Distance(siguiente, destino.position) <= 0.025f)
        {
            if (fase == Fase.HaciaPared &&
                comportamiento == Comportamiento.PasilloIdaYVuelta)
            {
                fase = Fase.HaciaPuerta;
            }
            else
            {
                fase = Fase.Terminado;
                gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HacerDanio(other);
    }

    private void OnTriggerStay(Collider other)
    {
        HacerDanio(other);
    }

    private static void HacerDanio(Collider other)
    {
        GuardHealth salud = other.GetComponentInParent<GuardHealth>();
        if (salud != null)
            salud.Hit();
    }
}
