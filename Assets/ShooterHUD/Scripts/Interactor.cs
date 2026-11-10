using UnityEngine;

public interface IInteractable
{
    string Prompt { get; }     // ex.: "Hackear terminal"
    float HoldTime { get; }    // segundos segurando a tecla (0 = instantâneo)
    void Interact(GameObject who);
}

/// Coloque na câmera/jogador. Aponta para um IInteractable, mostra o prompt e a barra.
public class Interactor : MonoBehaviour
{
    public Camera cam;
    public float range = 3f;
    public LayerMask mask = ~0;
    public KeyCode key = KeyCode.E;

    IInteractable current;
    float held; bool consumed;

    void Reset() { cam = Camera.main; }

    void Update()
    {
        var hud = HUDManager.Instance;
        if (!cam || !hud) return;

        IInteractable target = null;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, range, mask, QueryTriggerInteraction.Ignore))
            target = hit.collider.GetComponentInParent<IInteractable>();

        if (target != current)
        {
            current = target; held = 0f; consumed = false;
            if (current != null) hud.ShowInteract($"[{key}] {current.Prompt}");
            else hud.HideInteract();
        }
        if (current == null) return;

        if (Input.GetKeyUp(key)) consumed = false;

        if (!consumed && Input.GetKey(key))
        {
            held += Time.deltaTime;
            float need = Mathf.Max(0.0001f, current.HoldTime);
            hud.SetInteractProgress(held / need);
            if (held >= need)
            {
                current.Interact(gameObject);
                consumed = true; held = 0f;
                hud.SetInteractProgress(1f);
            }
        }
        else if (!Input.GetKey(key))
        {
            held = Mathf.Max(0f, held - Time.deltaTime * 2f);
            hud.SetInteractProgress(consumed ? 1f : held / Mathf.Max(0.0001f, current.HoldTime));
        }
    }
}
