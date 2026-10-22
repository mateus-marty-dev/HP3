using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wasserwiderstand mit optionalem, volumenbasiertem Auftrieb.
/// Fuer Physikobjekte (Stein, Holz, Kuebel) mit Rigidbody + Collider.
/// Nicht auf dekorative Seerosen mit HP3_FloatOnPond setzen.
/// Unity 6: linearDamping/angularDamping.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class HP3_WaterPhysics : MonoBehaviour
{
    [Header("Referenz fuer die Eintauchtiefe")]
    [Tooltip("Collider des Hauptkoerpers; beim Kuebel NICHT den schwingenden Buegel waehlen.")]
    [SerializeField] private Collider bodyCollider;

    [Header("1 - Wasserwiderstand (jetzt testen)")]
    [Min(0f)][SerializeField] private float underwaterLinearDamping = 3f;
    [Min(0f)][SerializeField] private float underwaterAngularDamping = 4f;

    [Header("2 - Auftrieb (vorerst AUS lassen)")]
    [SerializeField] private bool enableBuoyancy = false;
    [Min(0f)]
    [Tooltip("Geschaetztes verdraengtes Volumen in m³: 0.005 = 5 Liter.")]
    [SerializeField] private float displacedVolumeM3 = 0.005f;
    [Range(0f, 2f)][SerializeField] private float buoyancyMultiplier = 1f;

    [Header("Testanzeige (wird im Play Mode aktualisiert)")]
    [SerializeField, Range(0f, 1f)] private float submergedFraction;

    private Rigidbody rb;
    private float initialLinearDamping;
    private float initialAngularDamping;

    // Mehrere Objekt-Collider duerfen dieselbe Wasserzone beruehren.
    // Der Widerstand endet erst, wenn auch der letzte Kontakt weg ist.
    private readonly Dictionary<HP3_WaterBody, HashSet<Collider>> waterContacts =
        new Dictionary<HP3_WaterBody, HashSet<Collider>>();

    private void Reset()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) body.useGravity = true;
        bodyCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        initialLinearDamping = rb.linearDamping;
        initialAngularDamping = rb.angularDamping;

        if (bodyCollider == null)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                if (!col.isTrigger && col.attachedRigidbody == rb)
                {
                    bodyCollider = col;
                    break;
                }
            }
        }

        if (bodyCollider == null)
            Debug.LogWarning($"{name}: Bitte den Body Collider zuweisen.", this);
    }

    public void RegisterWaterContact(HP3_WaterBody water, Collider col)
    {
        if (water == null || col == null) return;
        if (!waterContacts.TryGetValue(water, out HashSet<Collider> contacts))
        {
            contacts = new HashSet<Collider>();
            waterContacts.Add(water, contacts);
        }
        contacts.Add(col); // OnTriggerStay darf nicht mehrfach zaehlen.
    }

    public void UnregisterWaterContact(HP3_WaterBody water, Collider col)
    {
        if (water == null || col == null) return;
        if (!waterContacts.TryGetValue(water, out HashSet<Collider> contacts)) return;
        contacts.Remove(col);
        if (contacts.Count == 0) waterContacts.Remove(water);
    }

    private void FixedUpdate()
    {
        if (rb.isKinematic || bodyCollider == null || !bodyCollider.enabled)
        {
            RestoreOriginalDamping();
            submergedFraction = 0f;
            return;
        }

        Bounds b = bodyCollider.bounds;
        if (b.size.y < 0.0001f)
        {
            RestoreOriginalDamping();
            submergedFraction = 0f;
            return;
        }

        HP3_WaterBody activeWater = null;
        float surfaceY = float.NegativeInfinity;

        // Bei ueberschneidenden Wasserzonen die hoechste aktive Oberflaeche nehmen.
        foreach (var pair in waterContacts)
        {
            HP3_WaterBody water = pair.Key;
            if (water == null || !water.isActiveAndEnabled || pair.Value.Count == 0)
                continue;

            float y = water.GetSurfaceY(b.center, Time.fixedTime);
            if (y > surfaceY)
            {
                activeWater = water;
                surfaceY = y;
            }
        }

        if (activeWater == null)
        {
            submergedFraction = 0f;
            RestoreOriginalDamping();
            return;
        }

        // Erste, preiswerte Naeherung: eingetauchte Hoehe / Gesamt-Hoehe.
        // Ein Collider am Hauptkoerper ist deshalb sinnvoll.
        submergedFraction = Mathf.Clamp01(
            (surfaceY - b.min.y) / Mathf.Max(b.size.y, 0.001f));

        rb.linearDamping = Mathf.Lerp(
            initialLinearDamping, underwaterLinearDamping, submergedFraction);
        rb.angularDamping = Mathf.Lerp(
            initialAngularDamping, underwaterAngularDamping, submergedFraction);

        // Echter Auftrieb als Archimedische Kraft (vereinfachte Volumenschaetzung).
        // Standardmaessig deaktiviert: damit testen wir zuerst NUR Widerstand.
        if (enableBuoyancy && submergedFraction > 0f && displacedVolumeM3 > 0f)
        {
            Vector3 upwardForce = -Physics.gravity *
                (activeWater.Density * displacedVolumeM3 *
                 submergedFraction * buoyancyMultiplier);

            rb.AddForce(upwardForce, ForceMode.Force);
        }
    }

    private void RestoreOriginalDamping()
    {
        if (rb == null) return;
        rb.linearDamping = initialLinearDamping;
        rb.angularDamping = initialAngularDamping;
    }

    private void OnDisable()
    {
        waterContacts.Clear();
        submergedFraction = 0f;
        RestoreOriginalDamping();
    }
}
