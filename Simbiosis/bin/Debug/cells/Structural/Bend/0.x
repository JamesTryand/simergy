xof 0303txt 0032
template XSkinMeshHeader {
 <3cf169ce-ff7c-44ab-93c0-f78f62d172e2>
 WORD nMaxSkinWeightsPerVertex;
 WORD nMaxSkinWeightsPerFace;
 WORD nBones;
}

template VertexDuplicationIndices {
 <b8d65549-d7c9-4995-89cf-53a9a8b031e3>
 DWORD nIndices;
 DWORD nOriginalVertices;
 array DWORD indices[nIndices];
}

template SkinWeights {
 <6f0d123b-bad2-4167-a0d0-80224f25fabb>
 STRING transformNodeName;
 DWORD nWeights;
 array DWORD vertexIndices[nWeights];
 array FLOAT weights[nWeights];
 Matrix4x4 matrixOffset;
}


Frame Scene_Root {
 

 Frame bend {
  

  FrameTransformMatrix {
   1.000000,0.000000,-0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.048214,0.000000,1.000000;;
  }

  Mesh bend_obj {
   25;
   0.000000;-0.050000;0.000000;,
   0.083862;0.234659;-0.086603;,
   -0.100000;-0.050000;-0.000000;,
   0.249311;0.292298;0.000000;,
   -0.050000;-0.050000;0.086603;,
   -0.050733;0.098011;0.086603;,
   0.050000;-0.050000;0.086603;,
   0.047076;0.077197;0.086603;,
   0.100000;-0.050000;0.000000;,
   0.095981;0.066790;0.000000;,
   0.050000;-0.050000;-0.086603;,
   0.047076;0.077197;-0.086603;,
   -0.050000;-0.050000;-0.086603;,
   -0.050733;0.098011;-0.086603;,
   -0.099638;0.108418;-0.000000;,
   0.249311;0.247548;0.077509;,
   0.249311;0.158048;0.077509;,
   0.249311;0.113298;0.000000;,
   0.249311;0.158048;-0.077509;,
   0.249311;0.247548;-0.077509;,
   0.083862;0.234659;0.086603;,
   0.054435;0.275083;0.000000;,
   0.142714;0.153811;0.086603;,
   0.172141;0.113387;0.000000;,
   0.142714;0.153811;-0.086603;;
   25;
   3;4,2,0;,
   4;2,4,5,14;,
   6;15,16,17,18,19,3;,
   3;6,4,0;,
   4;4,6,7,5;,
   4;1,21,3,19;,
   3;8,6,0;,
   4;6,8,9,7;,
   4;24,1,19,18;,
   3;10,8,0;,
   4;8,10,11,9;,
   4;23,24,18,17;,
   3;12,10,0;,
   4;10,12,13,11;,
   4;22,23,17,16;,
   3;2,12,0;,
   4;12,2,14,13;,
   4;20,22,16,15;,
   4;14,5,20,21;,
   4;5,7,22,20;,
   4;7,9,23,22;,
   4;9,11,24,23;,
   4;11,13,1,24;,
   4;13,14,21,1;,
   4;21,20,15,3;;

   MeshNormals {
    96;
    -0.328329;-0.756550;0.565537;,
    -0.655292;-0.755375;0.000000;,
    0.000000;-1.000000;0.000000;,
    -0.655292;-0.755375;0.000000;,
    -0.328329;-0.756550;0.565537;,
    -0.446168;0.179861;0.876689;,
    -0.927474;0.373887;0.000000;,
    0.445212;0.895425;0.000000;,
    0.503413;0.453444;-0.735502;,
    0.546498;-0.424578;-0.721855;,
    0.330966;-0.752504;0.569385;,
    -0.328329;-0.756550;0.565537;,
    0.000000;-1.000000;0.000000;,
    -0.328329;-0.756550;0.565537;,
    0.330966;-0.752504;0.569385;,
    0.417355;-0.210690;0.883982;,
    -0.446168;0.179861;0.876689;,
    0.523586;-0.851973;0.000000;,
    0.546498;-0.424578;0.721855;,
    0.503413;0.453444;0.735502;,
    0.665155;-0.746706;0.000000;,
    0.330966;-0.752504;0.569385;,
    0.000000;-1.000000;0.000000;,
    0.330966;-0.752504;0.569385;,
    0.665155;-0.746706;0.000000;,
    0.892698;-0.450655;0.000000;,
    0.417355;-0.210690;0.883982;,
    0.503413;0.453444;-0.735502;,
    0.445212;0.895425;0.000000;,
    -0.426032;0.904708;0.000000;,
    0.330966;-0.752504;-0.569385;,
    0.665155;-0.746706;0.000000;,
    0.000000;-1.000000;0.000000;,
    0.665155;-0.746706;0.000000;,
    0.330966;-0.752504;-0.569385;,
    0.417355;-0.210690;-0.883982;,
    0.892698;-0.450655;0.000000;,
    -0.189336;0.450001;-0.872726;,
    0.546498;-0.424578;-0.721855;,
    0.503413;0.453444;-0.735502;,
    -0.328329;-0.756550;-0.565537;,
    0.330966;-0.752504;-0.569385;,
    0.000000;-1.000000;0.000000;,
    0.330966;-0.752504;-0.569385;,
    -0.328329;-0.756550;-0.565537;,
    -0.446168;0.179861;-0.876689;,
    0.417355;-0.210690;-0.883982;,
    -0.189336;0.450001;-0.872726;,
    0.186346;-0.470140;-0.862695;,
    0.523586;-0.851973;0.000000;,
    -0.655292;-0.755375;0.000000;,
    -0.328329;-0.756550;-0.565537;,
    0.000000;-1.000000;0.000000;,
    -0.328329;-0.756550;-0.565537;,
    -0.655292;-0.755375;0.000000;,
    -0.927474;0.373887;0.000000;,
    -0.446168;0.179861;-0.876689;,
    0.546498;-0.424578;-0.721855;,
    0.186346;-0.470140;-0.862695;,
    0.330572;-0.943781;0.000000;,
    -0.927474;0.373887;0.000000;,
    -0.446168;0.179861;0.876689;,
    -0.189336;0.450001;0.872726;,
    -0.426032;0.904708;0.000000;,
    -0.446168;0.179861;0.876689;,
    0.417355;-0.210690;0.883982;,
    0.186346;-0.470140;0.862695;,
    -0.189336;0.450001;0.872726;,
    0.417355;-0.210690;0.883982;,
    0.892698;-0.450655;0.000000;,
    0.330572;-0.943781;0.000000;,
    0.186346;-0.470140;0.862695;,
    0.892698;-0.450655;0.000000;,
    0.417355;-0.210690;-0.883982;,
    0.186346;-0.470140;-0.862695;,
    0.330572;-0.943781;0.000000;,
    0.417355;-0.210690;-0.883982;,
    -0.446168;0.179861;-0.876689;,
    -0.189336;0.450001;-0.872726;,
    0.186346;-0.470140;-0.862695;,
    -0.446168;0.179861;-0.876689;,
    -0.927474;0.373887;0.000000;,
    -0.426032;0.904708;0.000000;,
    -0.189336;0.450001;-0.872726;,
    -0.426032;0.904708;0.000000;,
    -0.189336;0.450001;0.872726;,
    0.503413;0.453444;0.735502;,
    0.445212;0.895425;0.000000;,
    -0.189336;0.450001;0.872726;,
    0.186346;-0.470140;0.862695;,
    0.546498;-0.424578;0.721855;,
    0.503413;0.453444;0.735502;,
    0.186346;-0.470140;0.862695;,
    0.330572;-0.943781;0.000000;,
    0.523586;-0.851973;0.000000;,
    0.546498;-0.424578;0.721855;;
    25;
    3;0,1,2;,
    4;3,4,5,6;,
    6;19,18,17,9,8,7;,
    3;10,11,12;,
    4;13,14,15,16;,
    4;37,29,28,27;,
    3;20,21,22;,
    4;23,24,25,26;,
    4;48,47,39,38;,
    3;30,31,32;,
    4;33,34,35,36;,
    4;59,58,57,49;,
    3;40,41,42;,
    4;43,44,45,46;,
    4;92,93,94,95;,
    3;50,51,52;,
    4;53,54,55,56;,
    4;88,89,90,91;,
    4;60,61,62,63;,
    4;64,65,66,67;,
    4;68,69,70,71;,
    4;72,73,74,75;,
    4;76,77,78,79;,
    4;80,81,82,83;,
    4;84,85,86,87;;
   }

   MeshMaterialList {
    1;
    25;
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    0;

    Material DefaultLib_Material3 {
     1.000000;0.851000;0.700000;1.000000;;
     50.000000;
     1.000000;1.000000;1.000000;;
     0.000000;0.000000;0.000000;;
    }
   }
  }

  Frame skt0 {
   

   FrameTransformMatrix {
    -0.000000,-1.000000,-0.000000,0.000000,1.000000,-0.000000,0.000000,0.000000,-0.000000,0.000000,1.000000,0.000000,0.251985,0.213814,0.000000,1.000000;;
   }

   Mesh skt0_obj {
    4;
    -0.034388;0.000000;-0.034388;,
    -0.034388;0.000000;0.034388;,
    0.034388;0.000000;-0.034388;,
    0.034388;0.000000;0.034388;;
    1;
    4;0,1,3,2;;

    MeshNormals {
     4;
     0.000000;1.000000;0.000000;,
     0.000000;1.000000;0.000000;,
     0.000000;1.000000;0.000000;,
     0.000000;1.000000;0.000000;;
     1;
     4;0,1,2,3;;
    }

    MeshMaterialList {
     1;
     1;
     0;

     Material DefaultLib_Material {
      0.000000;1.000000;0.000000;1.000000;;
      50.000000;
      1.000000;1.000000;1.000000;;
      0.000000;0.000000;0.000000;;
     }
    }
   }
  }

  Frame chan0 {
   

   FrameTransformMatrix {
    1.000000,0.000000,-0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,-0.043973,0.002096,0.000000,1.000000;;
   }

   Mesh chan0_obj {
    12;
    0.120643;0.259128;0.000000;,
    0.144720;0.235050;-0.019659;,
    0.024791;-0.050441;-0.000000;,
    0.290721;0.236864;0.000000;,
    0.058842;-0.050441;0.019659;,
    0.011350;0.056751;0.019659;,
    0.058842;-0.050441;-0.019659;,
    0.011350;0.056751;-0.019659;,
    -0.022700;0.056751;-0.000000;,
    0.290721;0.202813;0.019659;,
    0.290721;0.202813;-0.019659;,
    0.144720;0.235050;0.019659;;
    11;
    3;2,6,4;,
    4;2,4,5,8;,
    3;9,10,3;,
    4;0,11,9,3;,
    4;4,6,7,5;,
    4;1,0,3,10;,
    4;7,8,0,1;,
    4;6,2,8,7;,
    4;11,1,10,9;,
    4;8,5,11,0;,
    4;5,7,1,11;;

    MeshNormals {
     42;
     0.341764;-0.650623;0.678150;,
     0.341764;-0.650623;-0.678150;,
     -0.563171;-0.826340;0.000000;,
     -0.563171;-0.826340;0.000000;,
     0.341764;-0.650623;0.678150;,
     0.423418;-0.049491;0.904581;,
     -0.994026;0.109140;0.000000;,
     0.716122;0.697975;0.000000;,
     0.703662;-0.310510;-0.639096;,
     0.703662;-0.310510;0.639096;,
     0.138652;-0.374341;-0.916867;,
     0.138652;-0.374341;0.916867;,
     0.716122;0.697975;0.000000;,
     0.341764;-0.650623;0.678150;,
     0.341764;-0.650623;-0.678150;,
     0.423418;-0.049491;-0.904581;,
     0.423418;-0.049491;0.904581;,
     0.703662;-0.310510;-0.639096;,
     0.716122;0.697975;0.000000;,
     -0.348808;0.937194;0.000000;,
     0.703662;-0.310510;0.639096;,
     0.138652;-0.374341;0.916867;,
     -0.348808;0.937194;0.000000;,
     0.341764;-0.650623;-0.678150;,
     -0.563171;-0.826340;0.000000;,
     -0.994026;0.109140;0.000000;,
     0.423418;-0.049491;-0.904581;,
     0.138652;-0.374341;-0.916867;,
     0.703662;-0.310510;0.639096;,
     0.703662;-0.310510;-0.639096;,
     -0.994026;0.109140;0.000000;,
     0.423418;-0.049491;0.904581;,
     0.138652;-0.374341;0.916867;,
     -0.348808;0.937194;0.000000;,
     0.423418;-0.049491;0.904581;,
     0.423418;-0.049491;-0.904581;,
     0.138652;-0.374341;-0.916867;,
     0.138652;-0.374341;0.916867;,
     0.423418;-0.049491;-0.904581;,
     -0.994026;0.109140;0.000000;,
     -0.348808;0.937194;0.000000;,
     0.138652;-0.374341;-0.916867;;
     11;
     3;2,1,0;,
     4;3,4,5,6;,
     3;9,8,7;,
     4;22,21,20,12;,
     4;13,14,15,16;,
     4;27,19,18,17;,
     4;38,39,40,41;,
     4;23,24,25,26;,
     4;11,10,29,28;,
     4;30,31,32,33;,
     4;34,35,36,37;;
    }

    MeshMaterialList {
     1;
     11;
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0;

     Material DefaultLib_Scene_Material {
      0.700000;0.700000;0.700000;1.000000;;
      50.000000;
      1.000000;1.000000;1.000000;;
      0.000000;0.000000;0.000000;;
     }
    }
   }
  }

  Frame chan2 {
   

   FrameTransformMatrix {
    1.000000,0.000000,-0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.039785,-0.004193,0.000000,1.000000;;
   }

   Mesh chan2_obj {
    13;
    -0.039150;-0.042675;0.000000;,
    0.118719;0.177075;-0.017321;,
    -0.056220;-0.042675;-0.000000;,
    0.208638;0.238062;0.000000;,
    -0.030615;-0.042675;0.017321;,
    0.008535;0.042675;0.017321;,
    -0.030615;-0.042675;-0.017321;,
    0.008535;0.042675;-0.017321;,
    -0.017070;0.042675;-0.000000;,
    0.208638;0.212457;0.017321;,
    0.208638;0.212457;-0.017321;,
    0.118719;0.177075;0.017321;,
    0.100614;0.195180;0.000000;;
    13;
    3;4,2,0;,
    4;2,4,5,8;,
    3;9,10,3;,
    3;6,4,0;,
    4;4,6,7,5;,
    4;1,12,3,10;,
    3;2,6,0;,
    4;6,2,8,7;,
    4;11,1,10,9;,
    4;8,5,11,12;,
    4;5,7,1,11;,
    4;7,8,12,1;,
    4;12,11,9,3;;

    MeshNormals {
     48;
     0.156493;-0.926299;0.342754;,
     -0.585646;-0.810567;0.000000;,
     0.000000;-1.000000;0.000000;,
     -0.585646;-0.810567;0.000000;,
     0.156493;-0.926299;0.342754;,
     0.373238;-0.238586;0.896532;,
     -0.851725;0.523990;0.000000;,
     0.457610;0.889153;0.000000;,
     0.789789;-0.261991;-0.554612;,
     0.789789;-0.261991;0.554612;,
     0.156493;-0.926299;-0.342754;,
     0.156493;-0.926299;0.342754;,
     0.000000;-1.000000;0.000000;,
     0.156493;-0.926299;0.342754;,
     0.156493;-0.926299;-0.342754;,
     0.373238;-0.238586;-0.896532;,
     0.373238;-0.238586;0.896532;,
     0.789789;-0.261991;-0.554612;,
     0.457610;0.889153;0.000000;,
     -0.598332;0.801248;0.000000;,
     -0.585646;-0.810567;0.000000;,
     0.156493;-0.926299;-0.342754;,
     0.000000;-1.000000;0.000000;,
     0.156493;-0.926299;-0.342754;,
     -0.585646;-0.810567;0.000000;,
     -0.851725;0.523990;0.000000;,
     0.373238;-0.238586;-0.896532;,
     0.251305;-0.358198;-0.899188;,
     0.789789;-0.261991;0.554612;,
     0.789789;-0.261991;-0.554612;,
     -0.851725;0.523990;0.000000;,
     0.373238;-0.238586;0.896532;,
     0.251305;-0.358198;0.899188;,
     -0.598332;0.801248;0.000000;,
     0.373238;-0.238586;0.896532;,
     0.373238;-0.238586;-0.896532;,
     0.251305;-0.358198;-0.899188;,
     0.251305;-0.358198;0.899188;,
     0.373238;-0.238586;-0.896532;,
     -0.851725;0.523990;0.000000;,
     -0.598332;0.801248;0.000000;,
     0.251305;-0.358198;-0.899188;,
     -0.598332;0.801248;0.000000;,
     0.251305;-0.358198;0.899188;,
     0.789789;-0.261991;0.554612;,
     0.457610;0.889153;0.000000;,
     0.251305;-0.358198;0.899188;,
     0.251305;-0.358198;-0.899188;;
     13;
     3;0,1,2;,
     4;3,4,5,6;,
     3;9,8,7;,
     3;10,11,12;,
     4;13,14,15,16;,
     4;27,19,18,17;,
     3;20,21,22;,
     4;23,24,25,26;,
     4;46,47,29,28;,
     4;30,31,32,33;,
     4;34,35,36,37;,
     4;38,39,40,41;,
     4;42,43,44,45;;
    }

    MeshMaterialList {
     1;
     13;
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0;

     Material DefaultLib_Scene_Material {
      0.700000;0.700000;0.700000;1.000000;;
      50.000000;
      1.000000;1.000000;1.000000;;
      0.000000;0.000000;0.000000;;
     }
    }
   }
  }

  Frame chan1 {
   

   FrameTransformMatrix {
    1.000000,0.000000,-0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.002096,0.000000,1.000000;;
   }

   Mesh chan1_obj {
    13;
    0.000000;-0.050000;0.000000;,
    0.130050;0.202700;-0.017321;,
    -0.020000;-0.050000;-0.000000;,
    0.247217;0.235379;0.000000;,
    0.010000;-0.050000;0.017321;,
    0.010000;0.050000;0.017321;,
    0.010000;-0.050000;-0.017321;,
    0.010000;0.050000;-0.017321;,
    -0.020000;0.050000;-0.000000;,
    0.247217;0.205379;0.017321;,
    0.247217;0.205379;-0.017321;,
    0.130050;0.202700;0.017321;,
    0.108837;0.223914;0.000000;;
    13;
    3;4,2,0;,
    4;2,4,5,8;,
    3;9,10,3;,
    3;6,4,0;,
    4;4,6,7,5;,
    4;1,12,3,10;,
    3;2,6,0;,
    4;6,2,8,7;,
    4;11,1,10,9;,
    4;8,5,11,12;,
    4;5,7,1,11;,
    4;7,8,12,1;,
    4;12,11,9,3;;

    MeshNormals {
     48;
     0.223607;-0.894427;0.387298;,
     -0.447214;-0.894427;0.000000;,
     0.000000;-1.000000;0.000000;,
     -0.447214;-0.894427;0.000000;,
     0.223607;-0.894427;0.387298;,
     0.442764;-0.149783;0.884039;,
     -0.942939;0.332965;0.000000;,
     0.648348;0.761344;0.000000;,
     0.723555;-0.326967;-0.607915;,
     0.723555;-0.326967;0.607915;,
     0.223607;-0.894427;-0.387298;,
     0.223607;-0.894427;0.387298;,
     0.000000;-1.000000;0.000000;,
     0.223607;-0.894427;0.387298;,
     0.223607;-0.894427;-0.387298;,
     0.442764;-0.149783;-0.884039;,
     0.442764;-0.149783;0.884039;,
     0.723555;-0.326967;-0.607915;,
     0.648348;0.761344;0.000000;,
     -0.464925;0.885350;0.000000;,
     -0.447214;-0.894427;0.000000;,
     0.223607;-0.894427;-0.387298;,
     0.000000;-1.000000;0.000000;,
     0.223607;-0.894427;-0.387298;,
     -0.447214;-0.894427;0.000000;,
     -0.942939;0.332965;0.000000;,
     0.442764;-0.149783;-0.884039;,
     0.186257;-0.396177;-0.899084;,
     0.723555;-0.326967;0.607915;,
     0.723555;-0.326967;-0.607915;,
     -0.942939;0.332965;0.000000;,
     0.442764;-0.149783;0.884039;,
     0.186257;-0.396177;0.899084;,
     -0.464925;0.885350;0.000000;,
     0.442764;-0.149783;0.884039;,
     0.442764;-0.149783;-0.884039;,
     0.186257;-0.396177;-0.899084;,
     0.186257;-0.396177;0.899084;,
     0.442764;-0.149783;-0.884039;,
     -0.942939;0.332965;0.000000;,
     -0.464925;0.885350;0.000000;,
     0.186257;-0.396177;-0.899084;,
     -0.464925;0.885350;0.000000;,
     0.186257;-0.396177;0.899084;,
     0.723555;-0.326967;0.607915;,
     0.648348;0.761344;0.000000;,
     0.186257;-0.396177;0.899084;,
     0.186257;-0.396177;-0.899084;;
     13;
     3;0,1,2;,
     4;3,4,5,6;,
     3;9,8,7;,
     3;10,11,12;,
     4;13,14,15,16;,
     4;27,19,18,17;,
     3;20,21,22;,
     4;23,24,25,26;,
     4;46,47,29,28;,
     4;30,31,32,33;,
     4;34,35,36,37;,
     4;38,39,40,41;,
     4;42,43,44,45;;
    }

    MeshMaterialList {
     1;
     13;
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0,
     0;

     Material DefaultLib_Scene_Material {
      0.700000;0.700000;0.700000;1.000000;;
      50.000000;
      1.000000;1.000000;1.000000;;
      0.000000;0.000000;0.000000;;
     }
    }
   }
  }
 }

 Frame corner-bevelled_cube {
  

  FrameTransformMatrix {
   1.000000,0.000000,-0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000;;
  }
 }
}