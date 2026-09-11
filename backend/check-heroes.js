const fs = require('fs');
const path = require('path');

// โหลดรายชื่อฮีโร่
const heroesData = JSON.parse(fs.readFileSync(path.join(__dirname, 'data', 'heroes.json'), 'utf8'));
const heroesPath = path.join(__dirname, 'public', 'images', 'heroes');

console.log('===========================================');
console.log('ROV Hero Images Checker');
console.log('===========================================\n');

console.log(`Total Heroes: ${heroesData.heroes.length}`);
console.log(`Checking images in: ${heroesPath}\n`);

let foundImages = 0;
let missingImages = [];

// ตรวจสอบแต่ละฮีโร่
heroesData.heroes.forEach(hero => {
    const imagePath = path.join(heroesPath, `${hero}.png`);
    
    if (fs.existsSync(imagePath)) {
        foundImages++;
        console.log(`✓ ${hero}.png`);
    } else {
        missingImages.push(hero);
        console.log(`✗ ${hero}.png - MISSING`);
    }
});

console.log('\n===========================================');
console.log('Summary');
console.log('===========================================');
console.log(`Found: ${foundImages}/${heroesData.heroes.length}`);
console.log(`Missing: ${missingImages.length}\n`);

if (missingImages.length > 0) {
    console.log('Missing Heroes:');
    missingImages.forEach(hero => {
        console.log(`  - ${hero}.png`);
    });
    console.log('\nPlease add these hero images to:');
    console.log(heroesPath);
} else {
    console.log('✓ All hero images are present!');
}

console.log('\n===========================================');
