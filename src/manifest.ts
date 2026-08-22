// The Feature Manifest: single source of truth for every explainable feature.
// Point MODEL_SRC at the GLB and rewrite these entries to swap in the real subject.
// Capture `position` and `normal` by opening the app with ?author and clicking the model.

export interface Feature {
  id: string;
  label: string;
  position: string; // "x y z" in model space
  normal: string; // "x y z" outward surface normal; camera framing is derived from this
  explanation: string;
  // GLB node names this feature covers, including mirrored halves. The web app
  // ignores these; the Quest app tints these meshes when the feature is focused.
  meshNames: string[];
}

// Anchor-less info about the Model — whole-skull facts that don't live at one spot
// (bone count, sutures, foramen magnum). Grounds the assistant but has no Callout and
// no camera framing. Add entries here to make the tutor answer these faithfully.
export interface Detail {
  id: string;
  label: string; // e.g. "Bone count", "Sutures"
  text: string; // authored, faithful answer the assistant may use
}

export const details: Detail[] = [
  {
    id: "bone-count",
    label: "How many bones",
    text: "The adult skull is made of 22 bones: 8 cranial bones that form the braincase (neurocranium) and 14 facial bones (viscerocranium). Most are fused together at immovable joints called sutures; the only freely movable bone is the mandible.",
  },
  {
    id: "sutures",
    label: "Sutures",
    text: "Sutures are the jagged, immovable joints where the flat bones of the cranium interlock. The main ones are the coronal suture (between the frontal and parietal bones), the sagittal suture (between the two parietal bones), and the lambdoid suture (between the parietal and occipital bones).",
  },
  {
    id: "foramen-magnum",
    label: "Foramen magnum",
    text: "The foramen magnum is the large opening at the base of the occipital bone. The brainstem passes through it to become the spinal cord, and it is where the skull rests on the first cervical vertebra (the atlas).",
  },
  {
    id: "cranium-vs-face",
    label: "Cranium vs. face",
    text: "The skull has two parts. The cranium (neurocranium) is the domed braincase that surrounds and protects the brain. The facial skeleton (viscerocranium) forms the front of the skull: the eye sockets, nasal cavity, cheeks, and jaws.",
  },
];

// Local model served from /public.
export const MODEL_SRC = "/SKULL.glb";

// Anchors captured in ?author mode against SKULL.glb.
export const features: Feature[] = [
  {
    id: "frontal",
    meshNames: ["Frontal bone"],
    label: "Frontal bone",
    position: "-0.018 0.148 0.083",
    normal: "-0.14 0.43 0.89",
    explanation:
      "The frontal bone forms your forehead and the roof of each eye socket, plus the brow ridges above the eyes. It's a single cranial bone that protects the front of the brain (the frontal lobes).",
  },
  {
    id: "parietal",
    meshNames: ["Parietal bone left", "Parietal bone right"],
    label: "Parietal bone",
    position: "-0.065 0.158 -0.050",
    normal: "-0.90 0.24 -0.37",
    explanation:
      "The two parietal bones form most of the roof and upper sides of the cranium — the large curved plates over the top of your head. Together they make up the bulk of the braincase and meet in the middle at the sagittal suture.",
  },
  {
    id: "temporal",
    meshNames: ["Temporal bone.l", "Temporal bone.r"],
    label: "Temporal bone",
    position: "-0.070 0.108 -0.035",
    normal: "-0.96 -0.18 -0.22",
    explanation:
      "The temporal bone sits at the side and base of the skull, around the temple and ear. It houses the middle and inner ear, and it forms the socket of the temporomandibular joint — the hinge where the lower jaw attaches.",
  },
  {
    id: "occipital",
    meshNames: ["Occipital bone"],
    label: "Occipital bone",
    position: "-0.011 0.110 -0.102",
    normal: "-0.117 -0.151 -0.982",
    explanation:
      "The occipital bone forms the back and base of the skull. It contains the foramen magnum, the large hole where the spinal cord leaves the brain, and it's where the skull balances on the top vertebra of the spine.",
  },
  {
    id: "zygomatic",
    meshNames: ["Zygomatic bone.l", "Zygomatic bone.r"],
    label: "Zygomatic bone",
    position: "-0.051 0.083 0.057",
    normal: "-0.87 0.06 0.49",
    explanation:
      "The zygomatic bone is your cheekbone. It creates the prominence of the cheek and forms part of the outer wall and floor of the eye socket. It links the facial bones to the temporal bone via the zygomatic arch.",
  },
  {
    id: "maxilla",
    meshNames: ["Maxilla bone.l", "Maxilla bone.r"],
    label: "Maxilla (upper jaw)",
    position: "-0.017 0.059 0.080",
    normal: "-0.27 0.40 0.87",
    explanation:
      "The maxilla is the upper jaw. It holds the upper row of teeth, forms most of the hard palate (the roof of the mouth), and contributes to the floor of the eye sockets and the sides of the nasal opening. Unlike the lower jaw, it's fixed in place.",
  },
  {
    id: "mandible",
    meshNames: ["Mandible bone"],
    label: "Mandible (lower jaw)",
    position: "-0.011 0.036 0.090",
    normal: "-0.29 -0.35 0.89",
    explanation:
      "The mandible is the lower jaw and the only bone of the skull that moves freely. It holds the lower teeth and hinges against the temporal bone at the temporomandibular joint, letting you chew and speak.",
  },
];
