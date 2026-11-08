export const categories=['CPU','MOTHERBOARD','RAM','GPU','PSU','CASE','STORAGE'] as const;
export type Category=typeof categories[number];
export const labels:Record<Category,string>={CPU:'Процессоры',MOTHERBOARD:'Материнские платы',RAM:'Оперативная память',GPU:'Видеокарты',PSU:'Блоки питания',CASE:'Корпуса',STORAGE:'Накопители'};
export interface Product{id:string;name:string;category:Category;active:boolean;priceRub:number;availableQuantity:number;specs:Record<string,unknown>}
export interface Entry{product:Product;sku:string;sourceUrl:string;sourceNotes:string;powerBasis:string;promotionPercent:number;promotionFrom:string|null;promotionTo:string|null;stockQuantity:number|null;version:number}
export interface User{id:string;login:string;role:string;customerType:'STANDARD'|'REGULAR'|'WHOLESALE'}
export interface Item{productId:string;category:Category;quantity:number}
export interface Quote{id:string;expiresAt:string;totalRub:number;available:boolean;lines:{productId:string;quantity:number;unitPriceRub:number;discountPercent:number;totalRub:number;available:boolean}[]}
export interface Cart{id:string;status:string;quote:Quote|null;reservationId:string|null;expiresAt:string|null}
export interface Metrics{orders:number;units:number;revenueRub:number;averageCheckRub:number}
export interface Analytics{summary:Metrics;previous:Metrics;change:Record<string,number|null>;timeline:{label:string;orders:number;units:number;revenueRub:number}[];topProducts:{name:string;units:number;revenueRub:number}[];categories:{category:Category;units:number;revenueRub:number}[]}

