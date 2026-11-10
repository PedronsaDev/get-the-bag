using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float Current { get; private set; }

    void Start() { Current = maxHealth; Push(); }

    public void TakeDamage(float amount)
    {
        if (Current <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        HUDManager.Instance.FlashDamage();
        Push();
        if (Current <= 0f) Debug.Log("Jogador morreu");   // sua lógica de morte aqui
    }

    public void Heal(float amount) { Current = Mathf.Min(maxHealth, Current + amount); Push(); }

    void Push() => HUDManager.Instance.SetHealth(Current, maxHealth);

#if UNITY_EDITOR
    void Update()   // teste rápido: Q = dano, F = cura
    {
        if (Input.GetKeyDown(KeyCode.Q)) TakeDamage(15f);
        if (Input.GetKeyDown(KeyCode.F)) Heal(20f);
    }
#endif
}
