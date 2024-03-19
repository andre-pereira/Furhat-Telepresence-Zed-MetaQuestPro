using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TCPFurhatComm;
using Oculus.Movement.Tracking;
using UnityEngine.Assertions;
using Oculus.Movement;

[DefaultExecutionOrder(-100000)]
public class Furhat : MonoBehaviour
{
    private float defaultAnimationDuration = 0.1f;
    private int GazeMultiplierPan = 100;
    private int GazeMultiplierTilt = 100;
    private int GazeMultiplierRoll = 100;
    private int GazeCalibratePan = 0;
    private int GazeCalibrateTilt = 0;
    private int GazeCalibrateRoll = 0;
    private bool bothEyesClose = false;
    public ZEDManager ZEDManager;

    public string FurhatIPAddress;

    public int timeToAnimateInMS = 50;
    private float sumDeltaTime = 0;

    [Header("Face Tracking")]
    public bool FaceAnimationsFromQuestPro = true;
    /// <summary>
    /// OVR face expressions component.
    /// </summary>
    [SerializeField]
    [Tooltip(FaceTrackingSystemTooltips.OVRFaceExpressions)]
    protected OVRFaceExpressions _ovrFaceExpressions;
    public float[] ExpressionWeights { get; private set; }
    public float thresholdToAnimate = 0.05f;


    [Header("Furhat Parameters")]
    [LabeledArray(typeof(BASICPARAMS))]
    public double[] BASICBlendshapeParameters = new double[Enum.GetNames(typeof(BASICPARAMS)).Length];
    private double[] previousBASICArray = new double[Enum.GetNames(typeof(BASICPARAMS)).Length];

    [LabeledArray(typeof(ARKITPARAMS))]
    public double[] ARKITBlendshapeParameters = new double[Enum.GetNames(typeof(ARKITPARAMS)).Length];
    private double[] previousARKITArray = new double[Enum.GetNames(typeof(ARKITPARAMS)).Length];

    [LabeledArray(typeof(CHARPARAMS))]
    public double[] CHARBlendshapeParameters = new double[Enum.GetNames(typeof(CHARPARAMS)).Length];
    private double[] previousCHARArray = new double[Enum.GetNames(typeof(CHARPARAMS)).Length];

    private float[] previousOVRArray = new float[Enum.GetNames(typeof(OVRFaceExpressions.FaceExpression)).Length - 2];

    private FurhatInterface furhat;

    [Header("Headpose Tracking From Transform")]
    public bool HeadPoseTrackingFromTransform = true;
    public Transform gazeHeadsetRotationTransform;
    public double thresholdToMoveNeck = 1;

    public double GazePan = 0;
    public double GazeTilt = 0;
    public double GazeRoll = 0;

    [Header("LipSync from Microphone")]
    [SerializeField]
    public bool RealTimeLipSyncActivated = true;
    public double[] lipSyncAnimationMultiplier = new double[14];
    public double lipSynchParamThreshold = 0;
    public OVRLipSyncContext lipSyncContext;
    private int currentViseme = 0;

    [Tooltip("Blendshape index to trigger for each viseme.")]
    public BASICPARAMS[] visemeToBlendTargets = (BASICPARAMS[])Enum.GetValues(typeof(BASICPARAMS));


    private void Awake()
    {
        Assert.IsNotNull(_ovrFaceExpressions);
        ExpressionWeights = new float[(int)OVRFaceExpressions.FaceExpression.Max];

        //test whether a FurhatSettings.txt file exists
        if (System.IO.File.Exists("FurhatSettings.txt"))
        {
            //put all lines of the file into an array
            string[] lines = System.IO.File.ReadAllLines("FurhatSettings.txt");

            //read the first line of the file and set the FurhatIPAddress to the value
            FurhatIPAddress = lines[0].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0];

            //read the second line of the file and set whether the RealTimeLipSyncActivated is true or false
            RealTimeLipSyncActivated = bool.Parse(lines[1].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read the third line of the file and set the timeToAnimateInMS to the value
            timeToAnimateInMS = int.Parse(lines[2].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read the fourth line of the file and set the thresholdToAnimate to the value
            thresholdToAnimate = float.Parse(lines[3].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //change default animation duration
            defaultAnimationDuration = float.Parse(lines[4].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read the Zed camera resolution
            ZEDManager.resolution = (sl.RESOLUTION)int.Parse(lines[5].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read the Zed Camera FPS
            ZEDManager.streamingTargetFramerate = int.Parse(lines[6].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read lipsync multipliers
            for (int i = 0; i < 14; i++)
            {
                lipSyncAnimationMultiplier[i] = double.Parse(lines[7 + i].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
            }

            //read GazeMultiplier for Pan, Tilt and Roll
            GazeMultiplierPan = int.Parse(lines[21].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
            GazeMultiplierTilt = int.Parse(lines[22].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
            GazeMultiplierRoll = int.Parse(lines[23].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read Gaze Calibration values
            GazeCalibratePan = int.Parse(lines[24].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
            GazeCalibrateTilt = int.Parse(lines[25].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
            GazeCalibrateRoll = int.Parse(lines[26].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);

            //read a boolean value to check whether both eyes should close at the same time
            bothEyesClose = bool.Parse(lines[27].Split((char[])null, StringSplitOptions.RemoveEmptyEntries)[0]);
        }
    }

    void Start()
    {

        furhat = new FurhatInterface(FurhatIPAddress, nameForSkill: "Unity App");
        resetAllParameters();
        furhat.EnableMicroexpressions(false);
        //GazePan = 30;
        //ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_IN_LEFT] = .5;
        //ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_OUT_RIGHT] = .5;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            //recenter the headset
            OVRManager.display.RecenterPose();
            print("Recentered");
        }
        else if (Input.GetKeyDown(KeyCode.L))
        {
            RealTimeLipSyncActivated = !RealTimeLipSyncActivated;
        }

        sumDeltaTime += Time.deltaTime * 1000;
        if (sumDeltaTime > timeToAnimateInMS)
        {
            bool lipSyncUpdated = false;
            if (RealTimeLipSyncActivated)
            {
                lipSyncUpdated = updateLipsyncBlendshapes();
            }

            updateFurhatHeadRotation();

            updateBlendshapesBASIC();
            //updateBlendshapesCHAR();

            if (FaceAnimationsFromQuestPro)
            {
                updateExpressionWeights();
                updateBlendshapesOVR(lipSyncUpdated);
            }
            
            updateBlendshapesARKIT();

            sumDeltaTime = 0;
        }

    }

    private void updateExpressionWeights()
    {
        if (ExpressionWeights == null || ExpressionWeights.Length != (int)OVRFaceExpressions.FaceExpression.Max)
        {
            ExpressionWeights = new float[(int)OVRFaceExpressions.FaceExpression.Max];
        }

        if (_ovrFaceExpressions.enabled &&
            _ovrFaceExpressions.FaceTrackingEnabled &&
            _ovrFaceExpressions.ValidExpressions)
        {
            for (var expressionIndex = 0;
                    expressionIndex < (int)OVRFaceExpressions.FaceExpression.Max;
                    ++expressionIndex)
            {
                var blendshape = (OVRFaceExpressions.FaceExpression)expressionIndex;
                ExpressionWeights[expressionIndex] = _ovrFaceExpressions[blendshape];
            }
        }
    }

    private bool updateLipsyncBlendshapes()
    {

        bool valueUpdated = false;
        float biggestIntensity = 0;
        float currentIntensity;
        int visemeCandidate = 0;

        OVRLipSync.Frame frame = lipSyncContext.GetCurrentPhonemeFrame();

        for (int paramNumber = 0; paramNumber < frame.Visemes.Length; paramNumber++)
        {
            currentIntensity = frame.Visemes[paramNumber];
            if (currentIntensity > biggestIntensity)
            {
                biggestIntensity = currentIntensity;
                visemeCandidate = paramNumber;
            }
        }

        if (visemeCandidate != currentViseme)
        {
            if(currentViseme != 0)
                lipSyncBlendshapeUpdate(currentViseme -1, 0);
            if (visemeCandidate != 0)
                lipSyncBlendshapeUpdate(visemeCandidate -1, 1 * (float)lipSyncAnimationMultiplier[visemeCandidate]);
            currentViseme = visemeCandidate;
            valueUpdated = true;
        }

        return valueUpdated;
    }

    public void lipSyncBlendshapeUpdate(int paramNumber, float intensity)
    {
        BASICBlendshapeParameters[(int)visemeToBlendTargets[paramNumber]] = intensity;
    }


    private void updateFurhatHeadRotation()
    {
        if (HeadPoseTrackingFromTransform)
        {
            GazePan = (gazeHeadsetRotationTransform.rotation.y * GazeMultiplierPan) + GazeCalibratePan;
            GazeTilt = (gazeHeadsetRotationTransform.rotation.x * GazeMultiplierTilt) + GazeCalibrateTilt;
            GazeRoll = (gazeHeadsetRotationTransform.rotation.z * GazeMultiplierRoll) + GazeCalibrateRoll;
        }

        BASICBlendshapeParameters[(int)BASICPARAMS.NECK_PAN] =  - GazePan;
        BASICBlendshapeParameters[(int)BASICPARAMS.NECK_TILT] =  GazeTilt;
        BASICBlendshapeParameters[(int)BASICPARAMS.NECK_ROLL] = - GazeRoll;
    }

    private void updateBlendshapesOVR(bool inLypsync)
    {
        if (FaceAnimationsFromQuestPro)
        {
            for (int i = 0; i < previousOVRArray.Length; i++)
            {
                //if (Math.Abs(ExpressionWeights[i] - previousOVRArray[i]) > thresholdToAnimate)
                //{
                    changeARKITParameterFromOVR(inLypsync, i, ExpressionWeights[i]);
                    //previousOVRArray[i] = ExpressionWeights[i];
                //}
            }
        }
    }

    private void changeARKITParameterFromOVR(bool currentlyInLipSync, int code, float intensity)
    {
        switch (code)
        {
            case -1: break;
            case 0: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_DOWN_LEFT] = intensity; break; //Brow_Lowerer_L
            case 1: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_DOWN_RIGHT] = intensity; break; //Brow_Lowerer_R

            case 2: if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.CHEEK_PUFF] = intensity; break; //Cheek_Puff_L
            case 3: if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.CHEEK_PUFF] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.CHEEK_PUFF], intensity); break; //Cheek_Puff_R

            case 4: if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.CHEEK_SQUINT_LEFT] = intensity; break; //Cheek_Raiser_L
            case 5: if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.CHEEK_SQUINT_RIGHT] = intensity; break; //Cheek_Raiser_R

            case 6: /*ChangeParameter(CHARPARAMS.CHEEK_THINNER, duration, intensity, priority);*/ break; //Cheek_Suck_L
            case 7: break;//Cheek_Suck_R

            case 8: if (!currentlyInLipSync) 
                        ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SHRUG_LOWER] = intensity; 
                    else
                        ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SHRUG_LOWER] = 0;
                    break; //Chin_Raiser_B

            case 9:
                if (!currentlyInLipSync)
                    ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SHRUG_UPPER] = intensity;
                else
                    ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SHRUG_UPPER] = 0;
                break; //Chin_Raiser_T
            case 10: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_DIMPLE_LEFT] = intensity; 
                else
                    ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_DIMPLE_LEFT] = 0;
                break; //Dimpler_L
            case 11:
                if (!currentlyInLipSync)
                    ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_DIMPLE_RIGHT] = intensity;
                else ARKITBlendshapeParameters [(int)ARKITPARAMS.MOUTH_DIMPLE_RIGHT] = 0;
                break;//Dimpler_R
            case 12: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_LEFT] = intensity; break; //Eyes_Closed_L
            case 13: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_RIGHT] = intensity; break; //Eyes_Closed_R
            case 14: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_DOWN_LEFT] = intensity; break; //Eyes_Look_Down_L
            case 15: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_DOWN_RIGHT] = intensity; break; //Eyes_Look_Down_R
            case 16: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_OUT_LEFT] = intensity; break; //Eyes_Look_Left_L
            case 17: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_IN_RIGHT] = intensity; break; //Eyes_Look_Left_R
            case 18: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_IN_LEFT] = intensity; break; //Eyes_Look_Right_L
            case 19: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_OUT_RIGHT] = intensity; break; //Eyes_Look_Right_R
            case 20: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_UP_LEFT] = intensity; break; //Eyes_Look_Up_L
            case 21: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_LOOK_UP_RIGHT] = intensity; break; //Eyes_Look_Up_R

            case 22: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_INNER_UP] = intensity; break; //Inner_Brow_Raiser_L
            case 23: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_INNER_UP] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_INNER_UP],intensity); break;//Inner_Brow_Raiser_R

            case 24: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_OPEN] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_OPEN] = 0;
                break; //Jaw_Drop
            case 25: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_LEFT] = 0;
                break; //Jaw_Sideways_Left
            case 26: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_RIGHT] = 0;
                break; //Jaw_Sideways_Right
            case 27: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_FORWARD] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.JAW_FORWARD] = 0;
                break; //Jaw_Thrust
            case 28: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_SQUINT_LEFT] = intensity; break; //Lid_Tightener_L
            case 29: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_SQUINT_RIGHT] = intensity; break; //Lid_Tightener_R
            case 30: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FROWN_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FROWN_LEFT] = 0;
                break; //Lip_Corner_Depressor_L
            case 31: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FROWN_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FROWN_RIGHT] = 0;
                break; //Lip_Corner_Depressor_R
            case 32: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SMILE_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SMILE_LEFT] = 0;
                break; //Lip_Corner_Puller_L
            case 33: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SMILE_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_SMILE_RIGHT] = 0;
                break; //Lip_Corner_Puller_R

            //Repeated from (38,39), (48,49) and 51, 52
            case 34: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = 0;
                break; //Lip_Funneler_LB
            case 35: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL], intensity); 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = 0;
                break;//Lip_Funneler_LT
            case 36: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL], intensity); 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = 0;
                break; //Lip_Funneler_RB
            case 37: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL], intensity); 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_FUNNEL] = 0;
                break;//Lip_Funneler_RT

            case 38: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PRESS_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PRESS_LEFT] = 0;
                break; //Lip_Pressor_L
            case 39: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PRESS_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PRESS_RIGHT] = 0;
                break; //Lip_Pressor_R

            case 40: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PUCKER] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PUCKER] = 0;
                break;//Lip_Pucker_L
            case 41: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PUCKER] = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PUCKER], intensity); 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_PUCKER] = 0;
                break; ;//Lip_Pucker_R

            case 42: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_STRETCH_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_STRETCH_LEFT] = 0;
                break;//Lip_Stretcher_L
            case 43: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_STRETCH_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_STRETCH_RIGHT] = 0;
                break;//Lip_Stretcher_R
            case 44: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_LOWER] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_LOWER] = 0;
                break;//Lip_Suck_LB
            case 45: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_UPPER] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_UPPER] = 0;
                break;//Lip_Suck_LT
            case 46: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_LOWER] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_LOWER] = 0;
                break; //Lip_Suck_RB
            case 47: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_UPPER] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_ROLL_UPPER] = 0;
                break;  //Lip_Suck_RT

            case 48: break; //Lip_Tightener_L
            case 49: break; //Lip_Tightener_R

            case 50: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_CLOSE] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_CLOSE] = 0;
                break;//Lips_Toward
            case 51: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LOWER_DOWN_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LOWER_DOWN_LEFT] = 0;
                break;//Lower_Lip_Depressor_L
            case 52: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LOWER_DOWN_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LOWER_DOWN_RIGHT] = 0;
                break;//Lower_Lip_Depressor_R
            case 53: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_LEFT] = 0;
                break;//Mouth_Left
            case 54: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_RIGHT] = 0;
                break;//Mouth_Right
            case 55: ARKITBlendshapeParameters[(int)ARKITPARAMS.NOSE_SNEER_LEFT] = intensity; break;//Nose_Wrinkler_L
            case 56: ARKITBlendshapeParameters[(int)ARKITPARAMS.NOSE_SNEER_RIGHT] = intensity; break;//Nose_Wrinkler_R
            case 57: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_OUTER_UP_LEFT] = intensity; break;//Outer_Brow_Raiser_L
            case 58: ARKITBlendshapeParameters[(int)ARKITPARAMS.BROW_OUTER_UP_RIGHT] = intensity; break;//Outer_Brow_Raiser_R
            case 59: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_WIDE_LEFT] = intensity; break;//Upper_Lid_Raiser_L
            case 60: ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_WIDE_RIGHT] = intensity; break;//Upper_Lid_Raiser_R
            case 61: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_UPPER_UP_LEFT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_UPPER_UP_LEFT] = 0;
                break;//Upper_Lip_Raiser_L
            case 62: 
                if (!currentlyInLipSync) ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_UPPER_UP_RIGHT] = intensity; 
                else ARKITBlendshapeParameters[(int)ARKITPARAMS.MOUTH_UPPER_UP_RIGHT] = 0;
                break;//Upper_Lip_Raiser_R
            default:
                break;
        }
    }

    private void updateBlendshapesBASIC()
    {
        List<BASICPARAMS> parameters = new List<BASICPARAMS>();
        List<float> floats = new List<float>();
        for (int i = 0; i < BASICBlendshapeParameters.Length; i++)
        {
            if (Math.Abs(BASICBlendshapeParameters[i] - previousBASICArray[i]) > thresholdToAnimate)
            {
                parameters.Add((BASICPARAMS)i);
                floats.Add((float)BASICBlendshapeParameters[i]);
                previousBASICArray[i] = BASICBlendshapeParameters[i];
            }
        }
        if (parameters.Count > 0)
        {
            furhat.ChangeParameters(parameters, floats, defaultAnimationDuration);
            //print("Number of parameters changed: " + parameters.Count);
        }
    }

    private void updateBlendshapesARKIT()
    {
        List<ARKITPARAMS> parameters = new List<ARKITPARAMS>();
        List<float> floats = new List<float>();

        if (bothEyesClose == true)
        {
            double maxValueBlink = Math.Max(ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_LEFT], ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_RIGHT]);
            ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_LEFT] = maxValueBlink;
            ARKITBlendshapeParameters[(int)ARKITPARAMS.EYE_BLINK_RIGHT] = maxValueBlink;
        }

        for (int i = 0; i < ARKITBlendshapeParameters.Length; i++)
        {
            if (Math.Abs(ARKITBlendshapeParameters[i] - previousARKITArray[i]) > thresholdToAnimate)
            {
                parameters.Add((ARKITPARAMS)i);
                floats.Add((float)ARKITBlendshapeParameters[i]);
                previousARKITArray[i] = ARKITBlendshapeParameters[i];
            }
        }


        if (parameters.Count > 0)
        {
            furhat.ChangeParameters(parameters, floats, defaultAnimationDuration);
            //print("Number of parameters changed: " + parameters.Count);
        }
    }

    private void updateBlendshapesCHAR()
    {
        for (int i = 0; i < CHARBlendshapeParameters.Length; i++)
        {
            if (Math.Abs(CHARBlendshapeParameters[i] - previousCHARArray[i]) > thresholdToAnimate)
            {
                furhat.ChangeParameter((CHARPARAMS)i, defaultAnimationDuration, (float)CHARBlendshapeParameters[i]);
                previousCHARArray[i] = CHARBlendshapeParameters[i];
            }
        }
    }



    private void resetAllParameters()
    {
        for (int i = 0; i < BASICBlendshapeParameters.Length; i++)
        {
            furhat.ChangeParameter((BASICPARAMS)i, defaultAnimationDuration, 0);
        }

        for (int i = 0; i < CHARBlendshapeParameters.Length; i++)
        {
            furhat.ChangeParameter((CHARPARAMS)i, defaultAnimationDuration, 0);
        }
        for (int i = 0; i < ARKITBlendshapeParameters.Length; i++)
        {
            furhat.ChangeParameter((ARKITPARAMS)i, defaultAnimationDuration, 0);
        }
    }

    private void OnApplicationQuit()
    {
        furhat.CloseConnection();
    }

}
