using System.Collections;
using UnityEngine;

public class DashController : MonoBehaviour
{
    public float dashSpeed = 10f;
    public float dashDuration = 0.5f;
    public float dashCooldown = 1.5f;

    private bool isDashing = false;
    private bool canDash = true;

    public bool IsDashing => isDashing;
    public float DashSpeed => dashSpeed;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)  &&  canDash)
        {
            StartCoroutine(Dash());
        }
    }

    private IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }
}
