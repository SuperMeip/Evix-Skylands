using UnityEngine;
using UnityEditor;
using Evix.Terrain.DataGeneration.Stars;

namespace Evix.Controllers {

  public class AstrologicalObjectController : MonoBehaviour {

    /// <summary>
    /// The astrological object data
    /// </summary>
    public StarMap.AstrologicalObject astrologicalObjectData
      = default;

    /// <summary>
    /// If this object has been set up
    /// </summary>
    bool isInitializedAsChild
      = false;

    /// <summary>
    /// This objects orbital pivot.
    /// </summary>
    GameObject orbitalPivot
      = null;

    // Start is called before the first frame update
    void Start() {
      if (astrologicalObjectData.scale != 0) {
        transform.localScale = new Vector3(astrologicalObjectData.scale, astrologicalObjectData.scale, astrologicalObjectData.scale);
      }
      for (int index = 0; index < transform.childCount; index++) {
        Transform child = transform.GetChild(index);
        AstrologicalObjectController childController = child.gameObject.GetComponent<AstrologicalObjectController>();
        if (childController != null && !childController.isInitializedAsChild) {
          addSatelite(childController);
          index--;
        }
      }
    }

    // Update is called once per frame
    void Update() {
      rotate();
      orbit();
    }

    /// <summary>
    /// orbit over a "year"
    /// </summary>
    void orbit() {
      if (orbitalPivot != null && astrologicalObjectData.isOrbiting) {
        float yearInSeconds = astrologicalObjectData.orbitalPeriod * World.HourInSeconds;
        float percentOfYearComplete = Time.deltaTime / yearInSeconds;
        orbitalPivot.transform.Rotate(astrologicalObjectData.orbitalAxis, 360 * percentOfYearComplete);
      }
    }

    /// <summary>
    /// rotate over a "day"
    /// </summary>
    void rotate() {
      /// rotate % for amount of the in game day that's passed
      if (astrologicalObjectData.isRotating) {
        float dayInSeconds = astrologicalObjectData.rotationalPeriod * World.HourInSeconds;
        float percentOfDayComplete = Time.deltaTime / dayInSeconds;
        transform.Rotate(astrologicalObjectData.rotationalAxis, 360 * percentOfDayComplete);
      }
    }

    /// <summary>
    /// add a child satelite to this object and set it up
    /// </summary>
    /// <param name="sateliteController"></param>
    void addSatelite(AstrologicalObjectController sateliteController) {
      /// parent this to the child satelite and add it's pivot
      astrologicalObjectData.satelites.Add(sateliteController.astrologicalObjectData);
      GameObject satelitePivot = new GameObject($"{sateliteController.gameObject.name}'s orbital pivot");
      satelitePivot.transform.position = transform.position;
      satelitePivot.transform.parent = transform;
      sateliteController.gameObject.transform.parent = satelitePivot.transform;
      sateliteController.orbitalPivot = satelitePivot;

      /// move the satelite into the inital position for it's orbital axis
      sateliteController.gameObject.transform.position 
        = satelitePivot.transform.position 
          + satelitePivot.transform.forward
            * sateliteController.astrologicalObjectData.distanceFromParent 
            * StarMap.AstrologicalUnit;
      satelitePivot.transform.rotation = Quaternion.FromToRotation(sateliteController.astrologicalObjectData.orbitalAxis, sateliteController.astrologicalObjectData.orbitalAxis + sateliteController.astrologicalObjectData.orbitalAxis);
      satelitePivot.transform.Rotate(sateliteController.astrologicalObjectData.orbitalAxis, sateliteController.astrologicalObjectData.initialOrbitDegree);
      sateliteController.astrologicalObjectData.parent = satelitePivot?.transform.parent.gameObject.GetComponentInParent<AstrologicalObjectController>()?.astrologicalObjectData;
      sateliteController.isInitializedAsChild = true;
    }

#if UNITY_EDITOR
    void OnDrawGizmos() {
      // draw orbit
      if (astrologicalObjectData.isOrbiting && orbitalPivot != null) {
        Handles.DrawWireDisc(
          orbitalPivot.transform.position,
          astrologicalObjectData.parent?.isOrbiting ?? false
            ? orbitalPivot.transform.up 
            : orbitalPivot.transform.rotation * astrologicalObjectData.orbitalAxis,
          astrologicalObjectData.distanceFromParent * StarMap.AstrologicalUnit
        );
      }

      // draw rotational axis
      if (astrologicalObjectData.isRotating) {
        Vector3 localRotationalAxis = transform.rotation * astrologicalObjectData.rotationalAxis;
        Handles.DrawLine(transform.position - localRotationalAxis * 1000, transform.position + localRotationalAxis * 1000);
      }
    }
#endif
  }
}