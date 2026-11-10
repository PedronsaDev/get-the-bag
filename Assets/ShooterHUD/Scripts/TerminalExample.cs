using UnityEngine;

/// Exemplo: coloque em qualquer objeto com Collider.
public class TerminalExample : MonoBehaviour, IInteractable
{
    public string Prompt => "Hackear terminal";
    public float HoldTime => 2f;
    public void Interact(GameObject who) => Debug.Log("Terminal hackeado!");
}
