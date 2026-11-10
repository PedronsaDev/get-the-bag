using System.Collections;
using UnityEngine;

/// Exemplo de arma ligada ao HUD. Adapte à sua arma existente
/// chamando HUDManager.Instance.SetAmmo / SetReloading nos mesmos pontos.
public class Weapon : MonoBehaviour
{
    public int magSize = 30;
    public int reserve = 120;
    public float fireRate = 10f;      // tiros por segundo
    public float reloadTime = 1.4f;

    int mag; float nextShot; bool reloading;

    void Start() { mag = magSize; PushAmmo(); }

    void Update()
    {
        if (Input.GetButton("Fire1")) TryShoot();
        if (Input.GetKeyDown(KeyCode.R)) StartCoroutine(Reload());
    }

    void TryShoot()
    {
        if (reloading || Time.time < nextShot || mag <= 0) return;
        nextShot = Time.time + 1f / fireRate;
        mag--;
        PushAmmo();
        // >>> seu raycast / projétil / som / efeito aqui <<<
    }

    IEnumerator Reload()
    {
        if (reloading || mag == magSize || reserve <= 0) yield break;
        reloading = true;
        HUDManager.Instance.SetReloading(true);
        yield return new WaitForSeconds(reloadTime);
        int take = Mathf.Min(magSize - mag, reserve);
        mag += take; reserve -= take;
        reloading = false;
        HUDManager.Instance.SetReloading(false);
        PushAmmo();
    }

    void PushAmmo() => HUDManager.Instance.SetAmmo(mag, reserve);
}
