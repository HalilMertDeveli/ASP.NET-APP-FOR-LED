document.addEventListener('DOMContentLoaded', () => {
    const canvas = document.createElement('canvas');
    canvas.id = 'rgb-dynamic-bg';
    canvas.style.position = 'fixed';
    canvas.style.top = '0';
    canvas.style.left = '0';
    canvas.style.width = '100vw';
    canvas.style.height = '100vh';
    canvas.style.zIndex = '-1';
    canvas.style.pointerEvents = 'none';
    
    // Insert at the beginning of the body
    document.body.insertBefore(canvas, document.body.firstChild);
    
    const ctx = canvas.getContext('2d');
    let width = window.innerWidth;
    let height = window.innerHeight;
    canvas.width = width;
    canvas.height = height;

    let mouseX = width / 2;
    let mouseY = height / 2;

    let targetX = mouseX;
    let targetY = mouseY;

    window.addEventListener('resize', () => {
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = width;
        canvas.height = height;
    });

    window.addEventListener('mousemove', (e) => {
        targetX = e.clientX;
        targetY = e.clientY;
    });

    // Positions for RGB blobs
    let robs = [
        { color: 'rgba(255, 0, 0, 0.4)', x: width / 2, y: height / 2, vx: 0, vy: 0, multX: 0.1, multY: -0.1 },   // Red
        { color: 'rgba(0, 255, 0, 0.4)', x: width / 2, y: height / 2, vx: 0, vy: 0, multX: -0.1, multY: 0.1 },  // Green
        { color: 'rgba(0, 0, 255, 0.4)', x: width / 2, y: height / 2, vx: 0, vy: 0, multX: -0.1, multY: -0.1 } // Blue
    ];

    function render() {
        ctx.clearRect(0, 0, width, height);
        
        ctx.fillStyle = '#0a0a0a'; // Dark base
        ctx.fillRect(0, 0, width, height);

        // Smooth mouse following
        mouseX += (targetX - mouseX) * 0.05;
        mouseY += (targetY - mouseY) * 0.05;

        // Draw RGB gradient blobs
        for (let i = 0; i < robs.length; i++) {
            let blob = robs[i];
            
            // Calculate blob offset relative to mouse center to add dynamic feel
            let offsetX = (mouseX - width / 2) * blob.multX * 5;
            let offsetY = (mouseY - height / 2) * blob.multY * 5;

            // Move blobs smoothly toward calculated position
            let destX = mouseX + offsetX;
            let destY = mouseY + offsetY;

            blob.x += (destX - blob.x) * 0.08;
            blob.y += (destY - blob.y) * 0.08;

            let gradient = ctx.createRadialGradient(blob.x, blob.y, 0, blob.x, blob.y, 500);
            gradient.addColorStop(0, blob.color);
            gradient.addColorStop(1, 'rgba(0,0,0,0)');

            ctx.fillStyle = gradient;
            ctx.fillRect(0, 0, width, height);
        }

        // Add a scanline effect for tech feel
        ctx.fillStyle = 'rgba(255, 255, 255, 0.02)';
        for(let j = 0; j < height; j += 4) {
            ctx.fillRect(0, j, width, 1);
        }

        requestAnimationFrame(render);
    }

    render();
});
