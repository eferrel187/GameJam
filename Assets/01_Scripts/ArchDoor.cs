using System.Collections;
using UnityEngine;

/// <summary>
/// Puerta doble dentro de un arco (Doors_RoundArch_L + Doors_RoundArch_R).
/// Cada hoja gira sobre su borde exterior y se abre hacia "openTowards".
/// La abre GuardDoorAndKey (puerta con llave) o KidnapSequence (puerta oscura).
/// </summary>
public class ArchDoor : MonoBehaviour
{
    public Transform leftLeaf;
    public Transform rightLeaf;
    [Tooltip("Dirección (mundo) hacia la que se abren las hojas.")]
    public Vector3 openTowards = Vector3.right;
    public float openAngle = 100f;
    public float openSeconds = 1.3f;

    public bool IsOpen { get; private set; }
    public bool IsMoving { get; private set; }

    /// Centro del hueco de la puerta (para distancias).
    public Vector3 Center
    {
        get
        {
            Bounds b = LeafBounds();
            return b.center;
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        GameAudio.Puerta(Center);
        StartCoroutine(OpenRoutine());
    }

    Leaf openedL, openedR;
    float openedAngle;

    /// Cierra la puerta (con el mismo giro, al revés) y vuelve a bloquear el paso.
    public void Close(float seconds = 0.9f)
    {
        if (!IsOpen) return;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine(seconds));
    }

    /// Vuelve a dejarla cerrada al instante (al reiniciar el nivel).
    public void ResetClosed()
    {
        StopAllCoroutines();
        if (openedL != null) openedL.Rotate(-openedAngle);
        if (openedR != null) openedR.Rotate(-openedAngle);
        openedAngle = 0f;
        IsOpen = IsMoving = false;
        SetLeafColliders(true);
    }

    IEnumerator CloseRoutine(float seconds)
    {
        IsMoving = true;
        float start = openedAngle;
        float done = 0f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float target = start * Mathf.SmoothStep(0f, 1f, t / seconds);
            float step = target - done;
            openedL?.Rotate(-step);
            openedR?.Rotate(-step);
            done = target;
            yield return null;
        }
        openedL?.Rotate(-(start - done));
        openedR?.Rotate(-(start - done));
        openedAngle = 0f;
        SetLeafColliders(true);
        IsOpen = IsMoving = false;
    }

    void SetLeafColliders(bool on)
    {
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if ((leftLeaf && c.transform.IsChildOf(leftLeaf)) || (rightLeaf && c.transform.IsChildOf(rightLeaf)) ||
                c.name.StartsWith("Colision_Hoja"))
                c.enabled = on;
    }

    IEnumerator OpenRoutine()
    {
        IsMoving = true;

        // Paso libre desde el primer momento.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (c.transform.IsChildOf(leftLeaf) || c.transform.IsChildOf(rightLeaf) ||
                c.name.StartsWith("Colision_Hoja"))
                c.enabled = false;

        Bounds all = LeafBounds();
        Leaf l = new Leaf(leftLeaf, all, openTowards);
        Leaf r = new Leaf(rightLeaf, all, openTowards);
        openedL = l; openedR = r; openedAngle = 0f;

        float done = 0f;
        for (float t = 0f; t < openSeconds; t += Time.deltaTime)
        {
            // Arranque lento, frenado suave (como una puerta pesada).
            float k = Mathf.SmoothStep(0f, 1f, t / openSeconds);
            float target = openAngle * k;
            float step = target - done;
            l.Rotate(step);
            r.Rotate(step);
            done = target;
            openedAngle = done;
            yield return null;
        }
        l.Rotate(openAngle - done);
        r.Rotate(openAngle - done);
        openedAngle = openAngle;

        IsMoving = false;
    }

    Bounds LeafBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Transform leaf in new[] { leftLeaf, rightLeaf })
        {
            if (leaf == null) continue;
            foreach (Renderer rd in leaf.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = rd.bounds; first = false; }
                else b.Encapsulate(rd.bounds);
            }
        }
        return b;
    }

    class Leaf
    {
        readonly Transform t;
        readonly Vector3 hinge;
        readonly float sign;

        public Leaf(Transform leaf, Bounds doorway, Vector3 towards)
        {
            t = leaf;
            if (t == null) return;

            Bounds b = new Bounds(t.position, Vector3.zero);
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            }

            // Bisagra en el borde exterior de la hoja (el más lejano al centro).
            bool alongZ = doorway.size.z >= doorway.size.x;
            Vector3 c = doorway.center;
            if (alongZ)
                hinge = new Vector3(b.center.x, b.center.y,
                    b.center.z < c.z ? b.min.z : b.max.z);
            else
                hinge = new Vector3(b.center.x < c.x ? b.min.x : b.max.x,
                    b.center.y, b.center.z);

            // Sentido de giro para que el borde libre vaya hacia "towards".
            Vector3 free = b.center - hinge;
            Vector3 moved = Quaternion.AngleAxis(10f, Vector3.up) * free - free;
            sign = Vector3.Dot(moved, towards) >= 0f ? 1f : -1f;
        }

        public void Rotate(float degrees)
        {
            if (t != null) t.RotateAround(hinge, Vector3.up, degrees * sign);
        }
    }
}
