const path = require("node:path");
const sharp = require("sharp");

const root = "C:/Users/juven/Desktop/Fruitables";
const assets = path.join(root, "docs/presentation/assets");

async function rasterizeSvg(source, output) {
  await sharp(source, { density: 220 })
    .resize({ width: 1800 })
    .flatten({ background: "#FFFFFF" })
    .png()
    .toFile(output);
}

async function cropBands(source, prefix, bands) {
  const metadata = await sharp(source).metadata();
  const width = metadata.width;
  const height = metadata.height;

  for (let i = 0; i < bands.length; i += 1) {
    const [start, end] = bands[i];
    const top = Math.max(0, Math.round(height * start));
    const bottom = Math.min(height, Math.round(height * end));
    await sharp(source)
      .extract({ left: 0, top, width, height: bottom - top })
      .flatten({ background: "#FFFFFF" })
      .png()
      .toFile(path.join(assets, prefix + "-" + (i + 1) + ".png"));
  }
}

async function cropFeatureTriptych(source) {
  const metadata = await sharp(source).metadata();
  const width = metadata.width;
  const height = metadata.height;
  const third = Math.floor(width / 3);
  const crops = [
    ["chat-feature-ui.png", 0, third],
    ["price-feature-ui.png", third, third],
    ["combo-feature-ui.png", third * 2, width - third * 2],
  ];

  for (const [name, left, cropWidth] of crops) {
    await sharp(source)
      .extract({ left, top: 0, width: cropWidth, height })
      .resize({ height: 1200 })
      .flatten({ background: "#FFFFFF" })
      .png()
      .toFile(path.join(assets, name));
  }
}

async function main() {
  const priceSvg = path.join(root, "docs/price-schedule/srs/price-e-create-apply-swimlane.svg");
  const priceFull = path.join(assets, "price-activity-source.png");
  await rasterizeSvg(priceSvg, priceFull);

  await cropBands(
    path.join(assets, "chat-activity-source.png"),
    "chat-activity",
    [[0, 0.37], [0.29, 0.72], [0.61, 1]]
  );
  await cropBands(
    priceFull,
    "price-activity",
    [[0, 0.43], [0.35, 0.76], [0.66, 1]]
  );
  await cropBands(
    path.join(assets, "combo-activity-source.png"),
    "combo-activity",
    [[0, 0.40], [0.32, 0.75], [0.64, 1]]
  );
  await cropFeatureTriptych(path.join(assets, "feature-ui-triptych.png"));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
