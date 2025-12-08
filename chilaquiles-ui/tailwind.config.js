/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{js,jsx,ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        chile: {
          50: '#fff7f5',
          100: '#ffe9e3',
          200: '#ffd0c2',
          300: '#ffb09a',
          400: '#ff7851',
          500: '#f44336',
          600: '#d73a2f',
          700: '#b52f26',
          800: '#92261f',
          900: '#761f19'
        },
        salsa: {
          verde: '#2e7d32',
          roja: '#c62828',
          mole: '#4e342e'
        }
      },
      boxShadow: {
        card: '0 10px 15px -3px rgba(0,0,0,0.1), 0 4px 6px -4px rgba(0,0,0,0.1)'
      }
    }
  },
  plugins: []
}
