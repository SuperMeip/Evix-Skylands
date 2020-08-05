using UnityEngine;

namespace Evix.Controllers.Testing {

  /// <summary>
  /// Test controller for moving objects consistently
  /// </summary>
  public class TestSledController : MonoBehaviour {

    /// <summary>
    /// If the sled is active
    /// </summary>
    public bool isActive = false;

    /// <summary>
    /// The move speed of the player
    /// </summary>
    public float moveSpeed = 10;

    /// <summary>
    /// The direction magnitude vector to move in
    /// </summary>
    public Vector3 moveDirection;

    // Update is called once per frame
    void Update() {
      if (isActive) {
        move();
      }
    }

    /// <summary>
    /// Move
    /// </summary>
    void move() {
      transform.position += moveDirection.normalized 
        * moveSpeed 
        * Time.deltaTime;
    }
  }
}
