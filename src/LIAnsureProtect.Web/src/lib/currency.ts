export const currencyFormatter = new Intl.NumberFormat("en-PH", {
  currency: "PHP",
  maximumFractionDigits: 0,
  style: "currency",
});

export function formatCurrency(value: number) {
  return currencyFormatter.format(value);
}
